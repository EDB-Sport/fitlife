#!/usr/bin/env bash
# Tests for tools/ci/release-gate.sh (issue #94), in a throwaway git repo with two services:
# changed-not-raised fails, changed-and-raised passes, unchanged passes, no tag passes,
# breaking-without-MAJOR fails, breaking-with-MAJOR passes, and a hotfix touching two services fails.
# Needs git and Docker (the breaking-change check runs oasdiff from its image). Usage: tools/ci/release-gate.test.sh
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo=$(mktemp -d)
trap 'rm -rf "$repo"' EXIT
failures=0
get_op='{"get":{"responses":{"200":{"description":"OK"}}}}'

# service <Name> <version>: writes src/<Name>/<Name>.csproj with that version
service() {
  mkdir -p "$repo/src/$1"
  printf '<Project Sdk="Microsoft.NET.Sdk.Web">\n  <PropertyGroup>\n    <Version>%s</Version>\n  </PropertyGroup>\n</Project>\n' "$2" > "$repo/src/$1/$1.csproj"
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

echo
if [ "$failures" -eq 0 ]; then echo "release-gate.sh works."; else echo "$failures check(s) failed."; fi
exit "$failures"
