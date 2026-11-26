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

# The plan compares with the revision each :develop image was built from (its label), not with the previous push, so a
# push whose run was cancelled or failed is made up by the next run (#340 review). The test stands in for the registry:
# "$DELIVERED_FILE" has one line "<image> <revision>" per delivered image.
delivered=$(mktemp)
cat > "$repo/lookup.sh" <<'LOOKUP'
#!/usr/bin/env bash
awk -v image="$1" '$1 == image { print $2 }' "$DELIVERED_FILE"
LOOKUP
chmod +x "$repo/lookup.sh"
trap 'rm -rf "$repo" "$delivered"' EXIT

# deliver <revision>: every service's :develop image was built from <revision> (empty: none was delivered yet)
deliver() {
  : > "$delivered"
  [ -z "${1:-}" ] || for s in bookingservice membershipservice memberportal; do echo "example/$s $1" >> "$delivered"; done
}
# plan <revision>: the plan for HEAD when the :develop images were built from <revision>, output in $out
plan() {
  deliver "${1:-}"
  out=$(cd "$repo" && DELIVERED_FILE="$delivered" DELIVERED_REVISION="$repo/lookup.sh" DOCKERHUB_NAMESPACE=example tools/ci/develop-plan.sh 2>&1)
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
cp "$here/develop-plan.sh" "$here/services.sh" "$here/changes.sh" "$here/delivered-revision.sh" "$repo/tools/ci/" 2>/dev/null
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

plan ""
check "no delivered image (nothing was ever pushed) plans every service" "image example/membershipservice:"
check "no delivered image plans the others too" "image example/bookingservice:"

plan "0000000000000000000000000000000000000000"
check "an image built from a commit this clone doesn't have plans every service" "image example/membershipservice:"

# A push whose run was cancelled: the images are still those of $base, so the middle push's service is planned by the next run
plan "$base"
check "a service changed since the delivered image is planned, even when a later push didn't touch it" "image example/bookingservice:"

# One service's image is missing while the others are current
deliver "$(git -C "$repo" rev-parse HEAD)"
sed -i '/membershipservice/d' "$delivered"
out=$(cd "$repo" && DELIVERED_FILE="$delivered" DELIVERED_REVISION="$repo/lookup.sh" DOCKERHUB_NAMESPACE=example tools/ci/develop-plan.sh 2>&1)
status=$?
check "a service without an image is planned" "image example/membershipservice:"
check "a service whose image is current is not" "image example/bookingservice:" absent

# The tags are never latest and never X.Y.Z (the output names exactly develop and sha-<short>)
check "no image is tagged latest" "latest" absent

# The default lookup, as it runs on the runner: executable in git, and reading the label through docker
mode=$(git -C "$here" ls-files -s -- "$here/delivered-revision.sh" | cut -c1-6)
if [ "$mode" = 100755 ]; then echo "PASS  delivered-revision.sh is executable in git"; else echo "FAIL  delivered-revision.sh is mode '$mode' in git, not 100755"; failures=$((failures + 1)); fi
mkdir -p "$repo/bin"
cat > "$repo/bin/docker" <<DOCKER
#!/usr/bin/env bash
echo '{"config":{"Labels":{"org.opencontainers.image.revision":"$(git -C "$repo" rev-parse HEAD)"}}}'
DOCKER
chmod +x "$repo/bin/docker"
out=$(cd "$repo" && PATH="$repo/bin:$PATH" DOCKERHUB_NAMESPACE=example tools/ci/develop-plan.sh 2>&1)
status=$?
check "the default lookup reads the revision label, so a current image is unchanged" "BookingService: unchanged since its :develop image"

outputs=$(mktemp)
deliver "$(git -C "$repo" rev-parse HEAD)"
out=$(cd "$repo" && GITHUB_OUTPUT="$outputs" DELIVERED_FILE="$delivered" DELIVERED_REVISION="$repo/lookup.sh" DOCKERHUB_NAMESPACE=example tools/ci/develop-plan.sh 2>&1)
status=$?
out="$out
$(cat "$outputs")"
rm -f "$outputs"
check "all images current writes an empty services list" "^services=\[\]$"

echo
if [ "$failures" -eq 0 ]; then echo "develop-plan.sh works."; else echo "$failures check(s) failed."; fi
exit "$failures"
