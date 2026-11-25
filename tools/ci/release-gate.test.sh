#!/usr/bin/env bash
# Tests for tools/ci/release-gate.sh (issue #94), in a throwaway git repo with two services:
# changed-not-raised fails, changed-and-raised passes, unchanged passes, no tag passes,
# breaking-without-MAJOR fails, breaking-with-MAJOR passes, and a hotfix touching two services fails.
# Shared code (issue #112): a changed libs/ project the service references (directly or not) or a changed
# root build file counts as a change of the service; a libs/ project it doesn't reference doesn't.
# Linked files (issue #182): a libs/ file the service links in as Content, None or Compile counts too.
# Needs git and Docker (the breaking-change check runs oasdiff from its image). Usage: tools/ci/release-gate.test.sh
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo=$(mktemp -d)
trap 'rm -rf "$repo"' EXIT
failures=0
get_op='{"get":{"responses":{"200":{"description":"OK"}}}}'

# references <Include>...: an ItemGroup with a ProjectReference per argument (none: nothing)
references() {
  [ "$#" -gt 0 ] || return 0
  printf '  <ItemGroup>\n'
  printf '    <ProjectReference Include="%s" />\n' "$@"
  printf '  </ItemGroup>\n'
}
# service <Name> <version> [<ProjectReference Include>...]: writes src/<Name>/<Name>.csproj
service() {
  local name=$1 version=$2
  shift 2
  mkdir -p "$repo/src/$name"
  {
    printf '<Project Sdk="Microsoft.NET.Sdk.Web">\n  <PropertyGroup>\n    <Version>%s</Version>\n  </PropertyGroup>\n' "$version"
    references "$@"
    printf '</Project>\n'
  } > "$repo/src/$name/$name.csproj"
}
# lib <Name> [<ProjectReference Include>...]: writes libs/<Name>/<Name>.csproj
lib() {
  local name=$1
  shift
  mkdir -p "$repo/libs/$name"
  { printf '<Project Sdk="Microsoft.NET.Sdk">\n'; references "$@"; printf '</Project>\n'; } > "$repo/libs/$name/$name.csproj"
}
# link_nlog <Name>: adds an ItemGroup that links libs/nlog.config into src/<Name>/<Name>.csproj as Content
# (portable: no sed -i, no \n in a replacement; tools/ci/tdd-check.test.sh)
link_nlog() {
  local csproj="$repo/src/$1/$1.csproj"
  {
    sed '$d' "$csproj"
    printf '  <ItemGroup>\n'
    printf '    <Content Include="$(RepoRoot)libs/nlog.config" Link="nlog.config" CopyToOutputDirectory="PreserveNewest" />\n'
    printf '  </ItemGroup>\n</Project>\n'
  } > "$csproj.tmp" && mv "$csproj.tmp" "$csproj"
}
# contract <file> <paths JSON>
contract() {
  printf '{"openapi":"3.1.1","info":{"title":"t","version":"1.0.0"},"paths":%s}\n' "$2" > "$repo/contracts/openapi/$1.json"
}
commit() { git -C "$repo" add -A && git -C "$repo" commit -qm "$1"; }

# expect <0|1> <head ref> <description> [grep pattern]
expect() {
  local out status
  out=$(cd "$repo" && HEAD_REF="$2" "$repo/tools/ci/release-gate.sh" 2>&1)
  status=$?
  if [ "$status" -eq "$1" ] && { [ -z "${4:-}" ] || grep -q "$4" <<<"$out"; }; then
    echo "PASS  $3"
  else
    echo "FAIL  $3 (exit $status, expected $1)"
    echo "$out" | tail -10
    failures=$((failures + 1))
  fi
}

git -C "$repo" init -q
git -C "$repo" config user.email test@example.invalid
git -C "$repo" config user.name test
mkdir -p "$repo/tools/ci" "$repo/contracts/openapi"
cp "$here/release-gate.sh" "$here/oasdiff-breaking.sh" "$here/services.sh" "$repo/tools/ci/" 2>/dev/null
service BookingService 1.0.0
service MembershipService 1.0.0
service MemberPortal.Web 1.0.0
contract bookingservice "{\"/classes\":$get_op,\"/reservations\":$get_op}"
commit "release 1.0.0"
git -C "$repo" tag bookingservice/v1.0.0
git -C "$repo" tag membershipservice/v1.0.0
git -C "$repo" tag memberportal/v1.0.0 # a UI's tag has no .web (deploy.yml, #42)

expect 0 release/1.1.0 "nothing changed since the tags passes" "MemberPortal.Web: unchanged since memberportal/v1.0.0"

echo "// change" > "$repo/src/BookingService/Reservations.cs"; commit "booking change"
expect 1 release/1.1.0 "a changed service without a raised version fails" "BookingService"

service BookingService 1.1.0; commit "raise booking"
expect 0 release/1.1.0 "a changed service with a raised version passes"

service NotificationService 0.1.0; commit "new service"
expect 0 release/1.1.0 "a service with no tag yet passes" "no notificationservice/v\* tag yet"

contract bookingservice "{\"/classes\":$get_op}"; commit "remove an endpoint"
expect 1 release/1.1.0 "a breaking change without a MAJOR fails" "MAJOR"

service BookingService 2.0.0; commit "raise booking to 2.0.0"
expect 0 release/2.0.0 "a breaking change with a MAJOR passes"

# Hotfixes start from the tags again
git -C "$repo" checkout -q -b hotfix/bookingservice-1.0.1 bookingservice/v1.0.0
cp "$here/release-gate.sh" "$here/oasdiff-breaking.sh" "$here/services.sh" "$repo/tools/ci/" 2>/dev/null
echo "// fix" > "$repo/src/BookingService/Fix.cs"; service BookingService 1.0.1
echo "// fix" > "$repo/src/MembershipService/Fix.cs"; service MembershipService 1.0.1
commit "hotfix two services"
expect 1 hotfix/bookingservice-1.0.1 "a hotfix touching two services fails" "only one service"

git -C "$repo" checkout -q -B hotfix/bookingservice-1.1.0 bookingservice/v1.0.0
cp "$here/release-gate.sh" "$here/oasdiff-breaking.sh" "$here/services.sh" "$repo/tools/ci/" 2>/dev/null
echo "// fix" > "$repo/src/BookingService/Fix.cs"; service BookingService 1.1.0; commit "hotfix raises MINOR"
expect 1 hotfix/bookingservice-1.1.0 "a hotfix that raises more than PATCH fails" "only PATCH"

git -C "$repo" checkout -q -B hotfix/bookingservice-1.0.1 bookingservice/v1.0.0
cp "$here/release-gate.sh" "$here/oasdiff-breaking.sh" "$here/services.sh" "$repo/tools/ci/" 2>/dev/null
echo "// fix" > "$repo/src/BookingService/Fix.cs"; service BookingService 1.0.1; commit "hotfix one service"
expect 0 hotfix/bookingservice-1.0.1 "a hotfix raising PATCH of one service passes"

# Shared code (#112): BookingService -> FitLife.Shared -> FitLife.Inner (indirect), MembershipService -> FitLife.Other,
# FitLife.Unused is referenced by nobody. A new release line, tagged at 1.5.0, starts from the 1.0.0 tags.
git -C "$repo" checkout -q -B release/1.6.0 bookingservice/v1.0.0
cp "$here/release-gate.sh" "$here/oasdiff-breaking.sh" "$here/services.sh" "$repo/tools/ci/" 2>/dev/null
lib FitLife.Inner
lib FitLife.Shared '..\FitLife.Inner\FitLife.Inner.csproj'
lib FitLife.Other
lib FitLife.Unused
echo '<Project />' > "$repo/Directory.Packages.props"
service BookingService 1.5.0 '$(RepoRoot)libs/FitLife.Shared/FitLife.Shared.csproj'
service MembershipService 1.5.0 '..\..\libs\FitLife.Other\FitLife.Other.csproj'
service MemberPortal.Web 1.5.0
commit "release 1.5.0 with shared libs"
git -C "$repo" tag bookingservice/v1.5.0
git -C "$repo" tag membershipservice/v1.5.0
git -C "$repo" tag memberportal/v1.5.0
expect 0 release/1.6.0 "nothing changed since the 1.5.0 tags passes" "BookingService: unchanged since bookingservice/v1.5.0"

echo "// change" > "$repo/libs/FitLife.Inner/Inner.cs"; commit "change an indirectly referenced lib"
expect 1 release/1.6.0 "an indirectly referenced libs/ change without a raised version fails" "BookingService.*libs/FitLife.Inner"
expect 1 release/1.6.0 "a libs/ change doesn't count for a service that doesn't reference it" "MembershipService: unchanged"

service BookingService 1.6.0 '$(RepoRoot)libs/FitLife.Shared/FitLife.Shared.csproj'; commit "raise booking"
expect 0 release/1.6.0 "a referenced libs/ change with a raised version passes" "BookingService: 1.5.0 -> 1.6.0"

echo "// change" > "$repo/libs/FitLife.Unused/Unused.cs"; commit "change an unreferenced lib"
expect 0 release/1.6.0 "a libs/ project no service references passes" "MembershipService: unchanged"

echo "// change" > "$repo/libs/FitLife.Other/Other.cs"; commit "change a directly referenced lib"
expect 1 release/1.6.0 "a directly referenced libs/ change (relative path) without a raised version fails" "MembershipService.*libs/FitLife.Other"
service MembershipService 1.6.0 '..\..\libs\FitLife.Other\FitLife.Other.csproj'; commit "raise membership"

service NotificationService 0.1.0
echo '<Project><ItemGroup /></Project>' > "$repo/Directory.Packages.props"; commit "a package change"
expect 1 release/1.6.0 "a root build file change fails a service that references no libs/" "MemberPortal.Web.*Directory.Packages.props"
expect 1 release/1.6.0 "a root build file change: a service with no tag yet still passes" "no notificationservice/v\* tag yet"
service MemberPortal.Web 1.6.0; commit "raise the portal"
expect 0 release/1.6.0 "a root build file change with every version raised passes"

# Linked shared files (#182): BookingService links libs/nlog.config into its output as Content, MembershipService doesn't.
git -C "$repo" tag bookingservice/v1.6.0
git -C "$repo" tag membershipservice/v1.6.0
git -C "$repo" tag memberportal/v1.6.0
git -C "$repo" checkout -q -B release/1.7.0
service BookingService 1.7.0 '$(RepoRoot)libs/FitLife.Shared/FitLife.Shared.csproj'
link_nlog BookingService
echo "<nlog />" > "$repo/libs/nlog.config"
commit "booking links libs/nlog.config"
git -C "$repo" tag bookingservice/v1.7.0
expect 0 release/1.7.0 "nothing changed since the 1.7.0 tag passes" "BookingService: unchanged since bookingservice/v1.7.0"

echo "<nlog><!-- change --></nlog>" > "$repo/libs/nlog.config"; commit "change libs/nlog.config"
expect 1 release/1.7.0 "a changed libs/ file linked into a service without a raised version fails" "BookingService.*libs/nlog.config"
expect 1 release/1.7.0 "a changed libs/ file doesn't count for a service that doesn't link it" "MembershipService: unchanged"
service BookingService 1.8.0 '$(RepoRoot)libs/FitLife.Shared/FitLife.Shared.csproj'
link_nlog BookingService
commit "raise booking"
expect 0 release/1.7.0 "a changed linked libs/ file with a raised version passes" "BookingService: 1.7.0 -> 1.8.0"

echo
if [ "$failures" -eq 0 ]; then echo "release-gate.sh works."; else echo "$failures check(s) failed."; fi
exit "$failures"
