#!/usr/bin/env bash
# Tests for tools/ci/develop-plan.sh (issue #340): which services a push to develop delivers images for,
# in a throwaway git repo. A service counts as changed by the rules of release-gate.sh (tools/ci/changes.sh).
# Needs only git. Usage: tools/ci/develop-plan.test.sh
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo=$(mktemp -d)
trap 'rm -rf "$repo"' EXIT
failures=0

service() { # service <Name> [<libs project> ...]: a service, with a ProjectReference to each libs/ project
  local name=$1
  shift
  mkdir -p "$repo/src/$name"
  {
    printf '<Project Sdk="Microsoft.NET.Sdk.Web">\n  <PropertyGroup>\n    <Version>1.0.0</Version>\n  </PropertyGroup>\n  <ItemGroup>\n'
    for lib in "$@"; do printf '    <ProjectReference Include="$(RepoRoot)libs/%s/%s.csproj" />\n' "$lib" "$lib"; done
    printf '  </ItemGroup>\n</Project>\n'
  } > "$repo/src/$name/$name.csproj"
  echo "FROM scratch" > "$repo/src/$name/Dockerfile"
}
lib() { mkdir -p "$repo/libs/$1"; printf '<Project Sdk="Microsoft.NET.Sdk"/>\n' > "$repo/libs/$1/$1.csproj"; }
commit() { git -C "$repo" add -A && git -C "$repo" commit -qm "$1" && git -C "$repo" rev-parse HEAD; }

# plan <before>: the plan for the push from <before> to HEAD, output in $out
plan() {
  out=$(cd "$repo" && BEFORE="$1" DOCKERHUB_NAMESPACE=example tools/ci/develop-plan.sh 2>&1)
  status=$?
}
check() { # check <description> <grep pattern> [absent]
  if [ "$status" -eq 0 ] && { if [ "${3:-}" = absent ]; then ! grep -q -- "$2" <<<"$out"; else grep -q -- "$2" <<<"$out"; fi; }; then
    echo "PASS  $1"
  else
    echo "FAIL  $1 (exit $status)"; echo "$out" | tail -10
    failures=$((failures + 1))
  fi
}

git -C "$repo" init -q
git -C "$repo" config user.email test@example.invalid
git -C "$repo" config user.name test
mkdir -p "$repo/tools/ci"
cp "$here/develop-plan.sh" "$here/services.sh" "$here/changes.sh" "$repo/tools/ci/" 2>/dev/null
lib FitLife.Messaging
lib FitLife.Other
service BookingService FitLife.Messaging
service MembershipService FitLife.Other
service MemberPortal.Web
echo "{}" > "$repo/global.json"
base=$(commit "base")

echo "// x" > "$repo/src/BookingService/Program.cs"
head=$(commit "booking")
plan "$base"
check "a change in src/<Service>/ plans that service" "image example/bookingservice: develop sha-${head:0:7}"
check "an unchanged service is not planned" "membershipservice" absent

echo "// x" > "$repo/libs/FitLife.Messaging/Bus.cs"
commit "lib" >/dev/null
plan "$head"
check "a change in a referenced libs/ project plans the service that references it" "image example/bookingservice:"
check "a libs/ change leaves a service that doesn't reference it" "membershipservice" absent

before_root=$(git -C "$repo" rev-parse HEAD)
echo '{"sdk":1}' > "$repo/global.json"
commit "root" >/dev/null
plan "$before_root"
check "a root build file plans every service" "image example/membershipservice:"
check "a root build file plans a UI, named without .web" "image example/memberportal:"

before_docs=$(git -C "$repo" rev-parse HEAD)
echo "x" > "$repo/README.md"
commit "docs" >/dev/null
plan "$before_docs"
check "a change outside any service plans nothing" "Nothing to deliver"

plan "0000000000000000000000000000000000000000"
check "the first push (no earlier commit) plans every service" "image example/membershipservice:"

# The tags are never latest and never X.Y.Z (the output names exactly develop and sha-<short>)
check "no image is tagged latest" "latest" absent

outputs=$(mktemp)
out=$(cd "$repo" && GITHUB_OUTPUT="$outputs" BEFORE="$before_docs" DOCKERHUB_NAMESPACE=example tools/ci/develop-plan.sh 2>&1)
status=$?
out="$out
$(cat "$outputs")"
rm -f "$outputs"
check "nothing to deliver writes an empty services list" "^services=\[\]$"

echo
if [ "$failures" -eq 0 ]; then echo "develop-plan.sh works."; else echo "$failures check(s) failed."; fi
exit "$failures"
