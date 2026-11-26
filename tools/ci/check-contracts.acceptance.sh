#!/usr/bin/env bash
# Executable acceptance check for the contract check (issues #90 and #491): tools/ci/check-contracts.sh
# passes on a clean checkout, fails after an endpoint or event record change without the regenerated
# contract, and passes again once the regenerated contract is in the commit.
# Needs dotnet, so it is not a *.test.sh (the ci-scripts job has no .NET). Usage: tools/ci/check-contracts.acceptance.sh
set -uo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
work=$(mktemp -d)
trap 'git -C "$root" worktree remove --force "$work" >/dev/null 2>&1; rm -rf "$work"' EXIT
git -C "$root" worktree add -q --detach "$work" HEAD
cd "$work" || exit 1
failures=0

expect() { # expect <pass|fail> <description>
  if tools/ci/check-contracts.sh >/dev/null 2>&1; then got=pass; else got=fail; fi
  if [ "$got" = "$1" ]; then echo "PASS  $2"; else echo "FAIL  $2: the check should $1"; failures=$((failures + 1)); fi
}

expect pass "a clean checkout has up-to-date contracts"

# An endpoint change, without committing the regenerated contract
perl -0pi -e 's/(app\.MapHealthChecks\("\/health"\);)/$1\napp.MapGet("\/contract-check", () => "x");/' src/BookingService/Program.cs
expect fail "an endpoint change without the regenerated contract fails"

# The same change with the regenerated contract committed (staged)
git add contracts/openapi/
expect pass "the same change with the regenerated contract passes"

# An event record change, without committing the regenerated schema (#491)
perl -0pi -e 's/(public static string RoutingKey)/public string? ContractCheck { get; init; }

    $1/' libs/FitLife.Messaging/Events/ReservationMade.cs
expect fail "an event record change without the regenerated schema fails"

# The same change with the regenerated schema committed (staged)
git add contracts/events/
expect pass "the same change with the regenerated schema passes"

echo
if [ "$failures" -eq 0 ]; then echo "The contract check works."; else echo "$failures check(s) failed."; fi
exit "$failures"
