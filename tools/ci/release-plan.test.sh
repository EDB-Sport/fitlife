#!/usr/bin/env bash
# Tests for tools/ci/release-plan.sh (issue #95): the dry run of a release on main, in a throwaway repo.
# It lists the tags and image names it would create for a changed service and skips an unchanged one.
# Needs only git. Usage: tools/ci/release-plan.test.sh
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo=$(mktemp -d)
trap 'rm -rf "$repo"' EXIT
failures=0

service() { # service <Name> <version>
  mkdir -p "$repo/src/$1"
  printf '<Project Sdk="Microsoft.NET.Sdk.Web">\n  <PropertyGroup>\n    <Version>%s</Version>\n  </PropertyGroup>\n</Project>\n' "$2" > "$repo/src/$1/$1.csproj"
  echo "FROM scratch" > "$repo/src/$1/Dockerfile"
}

# plan <release ref>: runs the dry run in the repo, output in $out
plan() {
  out=$(cd "$repo" && RELEASE_REF="$1" DOCKERHUB_NAMESPACE=example tools/ci/release-plan.sh 2>&1)
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
cp "$here/release-plan.sh" "$here/services.sh" "$repo/tools/ci/" 2>/dev/null
service BookingService 1.1.0
service MembershipService 1.0.0
service MemberPortal.Web 0.2.0
git -C "$repo" add -A && git -C "$repo" commit -qm "release 1.1.0"
git -C "$repo" tag membershipservice/v1.0.0

plan release/1.1.0
check "a changed service gets its tag" "tag bookingservice/v1.1.0"
check "a changed service gets its images" "image example/bookingservice: 1.1.0 1.1 1 latest"
check "a UI is named without .web" "tag memberportal/v0.2.0"
check "a 0.y.z version gets no major-only image tag, as docker/metadata-action makes none" "image example/memberportal: 0.2.0 0.2 latest"
check "the release gets its vx.y.z tag" "tag v1.1.0"
check "an unchanged service is skipped" "membershipservice/v1.0.0 exists, skipped"
check "an unchanged service gets no image" "image example/membershipservice" absent

plan hotfix/bookingservice-1.1.0
check "a hotfix gets no vx.y.z tag" "tag v1" absent

git -C "$repo" tag bookingservice/v1.1.0
git -C "$repo" tag memberportal/v0.2.0
git -C "$repo" tag v1.1.0
plan release/1.1.0
check "a re-run after the release plans nothing" "Nothing to release"

# A service without a Dockerfile can't be released: the plan fails and names it (issue #322)
service NoImageService 1.0.0
rm "$repo/src/NoImageService/Dockerfile"
plan release/1.1.0
if [ "$status" -ne 0 ] && grep -q "NoImageService 1.0.0 has no src/NoImageService/Dockerfile" <<<"$out"; then
  echo "PASS  a service without a Dockerfile fails the plan"
else
  echo "FAIL  a service without a Dockerfile fails the plan (exit $status)"; echo "$out" | tail -10
  failures=$((failures + 1))
fi
rm -rf "$repo/src/NoImageService"

# The same run in GitHub Actions writes empty outputs. bash 3.2 (macOS) treats an empty array as unbound under set -u (#87).
outputs=$(mktemp)
out=$(cd "$repo" && GITHUB_OUTPUT="$outputs" RELEASE_REF=release/1.1.0 DOCKERHUB_NAMESPACE=example tools/ci/release-plan.sh 2>&1)
status=$?
out="$out
$(cat "$outputs")"
rm -f "$outputs"
check "nothing to release writes an empty services list" "^services=\[\]$"
check "nothing to release writes empty tags" "^tags=$"

exit "$failures"
