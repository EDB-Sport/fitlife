#!/usr/bin/env bash
# Tests for tools/ci/image-versions.sh (issue #275), on throwaway trees: every image reference is a full
# version (or a digest), and the same image has the same version in Dockerfiles, compose files, Bicep and
# Testcontainers builders. The .NET runtime image (aspnet) may float (ADR 004).
# Usage: tools/ci/image-versions.test.sh   (exit code 0 = all cases pass)
set -uo pipefail

check="$(cd "$(dirname "$0")" && pwd)/image-versions.sh"
failures=0
tmp_root=$(mktemp -d)
trap 'rm -rf "$tmp_root"' EXIT
root=""

# fresh: an empty tree with the places the check reads
fresh() {
  root=$(mktemp -d "$tmp_root/tree.XXXXXX")
  mkdir -p "$root/deploy/compose" "$root/deploy/azure" "$root/src/Svc" "$root/tests/Svc.IntegrationTests"
}
compose() { printf 'services:\n  db:\n    image: %s\n' "$1" > "$root/deploy/compose/${2:-infrastructure.yml}"; }
bicep() { printf "param %s string = '%s'\n" "$1" "$2" > "$root/deploy/azure/main.bicep"; }
dockerfile() { printf '%s\n' "$@" > "$root/src/Svc/Dockerfile"; }
csharp() { printf 'var c = new MongoDbBuilder("%s").Build();\n' "$1" > "$root/tests/Svc.IntegrationTests/DbTests.cs"; }

# expect <0|1> <description> [grep pattern for the output]
expect() {
  local want=$1 desc=$2 pattern=${3:-} out status
  out=$("$check" "$root" 2>&1)
  status=$?
  if [ "$status" -eq "$want" ] && { [ -z "$pattern" ] || grep -qE -- "$pattern" <<<"$out"; }; then
    echo "PASS  $desc"
  else
    echo "FAIL  $desc (exit $status, wanted $want; pattern: ${pattern:-none})"
    sed 's/^/        /' <<<"$out"
    failures=$((failures + 1))
  fi
}

fresh
expect 0 "an empty tree passes"

fresh; compose 'mongo:8.0.38'; bicep mongoImage 'mongo:8.0.38'; csharp 'mongo:8.0.38'
dockerfile 'FROM mcr.microsoft.com/dotnet/sdk:10.0.203-alpine3.23 AS build' 'FROM build AS publish' 'FROM scratch'
expect 0 "full versions, the same everywhere, pass (stage names and scratch are not images)"

fresh; compose 'mongo:8'
expect 1 "a major-only tag in compose fails, naming file, line and image" 'deploy/compose/infrastructure.yml:3.*mongo:8'

fresh; dockerfile 'FROM mongo:8'
expect 1 "a major-only tag in a Dockerfile fails" 'src/Svc/Dockerfile:1.*mongo:8'

fresh; dockerfile 'FROM --platform=$BUILDPLATFORM mongo:8 AS x'
expect 1 "a FROM with a --platform flag is still checked" 'mongo:8'

fresh; bicep mongoImage 'mongo:8'
expect 1 "a major-only tag in a Bicep image parameter fails" 'deploy/azure/main.bicep:1.*mongo:8'

fresh; csharp 'mongo:8'
expect 1 "a major-only tag in a Testcontainers builder fails" 'tests/Svc.IntegrationTests/DbTests.cs:1.*mongo:8'

fresh; compose 'mongo'
expect 1 "an image without a tag fails" 'mongo'

fresh; compose 'mongo:latest'
expect 1 "latest fails" 'mongo:latest'

fresh; compose '${MONGO_IMAGE:-mongo:8}'
expect 1 "a compose default value is checked" 'mongo:8'

fresh; compose '${MONGO_IMAGE:-mongo:8.0.38}'; csharp 'mongo:8.0.38'
expect 0 "a full-version compose default passes"

fresh; compose 'mongo@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef'
expect 0 "a digest passes"

fresh; compose 'rabbitmq:4.4.1-management'
expect 0 "a full version with a suffix passes"

fresh; compose 'mongo:8.0.38'; csharp 'mongo:7.0.48'
expect 1 "mongo:7.0.48 against mongo:8.0.38 fails (the PR #199 case), naming both files and versions" \
  'mongo.*7\.0\.48.*8\.0\.38|mongo.*8\.0\.38.*7\.0\.48'
fresh; compose 'mongo:8.0.38'; csharp 'mongo:7.0.48'
out=$("$check" "$root" 2>&1)
if grep -q 'DbTests.cs:1' <<<"$out" && grep -q 'infrastructure.yml:3' <<<"$out"; then
  echo "PASS  the version mismatch names both files and lines"
else
  echo "FAIL  the version mismatch names both files and lines"; sed 's/^/        /' <<<"$out"; failures=$((failures + 1))
fi

fresh; compose 'mongo:8.0.38'; bicep mongoImage 'mongo:8.0.39'
expect 1 "compose against Bicep on different versions fails" '8\.0\.38.*8\.0\.39|8\.0\.39.*8\.0\.38'

fresh; compose 'nginx:1.30.7-alpine' a.yml; compose 'nginx:1.30.7' b.yml
expect 1 "the same version with another suffix counts as another version" 'nginx'

fresh; dockerfile 'FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine'
expect 0 "the aspnet runtime image may float (ADR 004)"

fresh; dockerfile 'FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine'; printf 'FROM mcr.microsoft.com/dotnet/aspnet:10.0.7-alpine3.23\n' > "$root/src/Svc/Dockerfile.b"
expect 0 "aspnet on two versions passes too"

fresh; dockerfile 'FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine'
expect 1 "the sdk image may not float" 'sdk:10.0-alpine'

fresh; printf "param serviceImage = 'albertarnason/bookingservice'\nparam serviceImageTag = '0.1.0'\n" > "$root/deploy/azure/main.bicepparam"
expect 0 "a Bicep image parameter whose version is in <name>Tag passes"

fresh; compose 'fitlife/bookingservice:local'
expect 0 "an image built from this repo (fitlife/...) is not checked"

fresh; compose 'mongo:8'; compose 'mongo:9' other.yml
expect 1 "every failure is reported, not just the first" 'mongo:9'

fresh; compose 'mongo:8.0.38'
expect 0 "a compose file under deploy/ is read once: one reference, not two" 'image-versions: 1 image reference\(s\) checked'

fresh; compose 'albertarnason/bookingservice:develop' compose.develop.yml
expect 0 "the :develop tag passes in compose.develop.yml (#340)"

fresh; compose 'albertarnason/bookingservice:develop' services.yml
expect 1 ":develop fails in any other compose file" 'services.yml:3.*develop'

fresh; compose 'albertarnason/bookingservice:sha-1a2b3c4' compose.develop.yml
expect 1 "only :develop is allowed in compose.develop.yml, not another floating tag" 'sha-1a2b3c4'

fresh; compose 'mongo:8' compose.develop.yml
expect 1 "compose.develop.yml may not float a third-party image" 'mongo:8'

# The real tree passes (the issue: today's develop passes, or the PR fixes what it finds).
if out=$("$check" 2>&1); then
  echo "PASS  the repository passes"
else
  echo "FAIL  the repository passes"; sed 's/^/        /' <<<"$out"; failures=$((failures + 1))
fi

echo
if [ "$failures" -gt 0 ]; then
  echo "image-versions.test.sh: $failures case(s) failed" >&2
  exit 1
fi
echo "image-versions.test.sh: all cases passed"
