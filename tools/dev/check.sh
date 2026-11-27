#!/usr/bin/env bash
# Runs locally what CI's required .NET and script jobs will say, so a push is green the first time (issue #603).
# Usage: tools/dev/check.sh [--all] [--skip <name>]...
#   all checks pass: prints "all checks green", exit 0
#   otherwise: one FAIL block per failed check (its name, the command, at most 15 lines of the log), exit 1
#   bad arguments or a missing dotnet: message on stderr, exit 2
# Checks, in CI's order: restore (locked), build (Release), format (verify), contracts (tools/ci/check-contracts.sh),
# test, and scripts (every tools/ci/*.test.sh and tools/dev/*.test.sh). A failed check does not stop the later ones.
# scripts runs only when the change touches tools/ or .github/ (git diff against origin/develop, the working
# tree and untracked files); --all forces it. --skip <name> leaves one check out; it beats --all.
# The log filter is the one in AGENTS.md ("Test and CI logs"): `error [A-Z]+[0-9]+:` and `Failed ` lines; a log
# without such a line shows its last lines instead.
# dotnet is found as `dotnet`, or `dotnet.exe` on WSL; set DOTNET to override (the test stubs it).
# CHECK_CONTRACTS, CHECK_SCRIPT_TESTS (space-separated) and CHECK_CHANGED_FILES (a file, one path per line) replace
# the contracts script, the *.test.sh list and the git diff. Tests: tools/dev/check.test.sh
# Not here: anything that needs Docker (mutation, tdd-check, image scans); CI keeps those.
set -euo pipefail

run_all=0
skips=" "
while [ $# -gt 0 ]; do
  case $1 in
    --all) run_all=1 ;;
    --skip)
      case ${2-} in
        restore | build | format | contracts | test | scripts) skips="$skips$2 " ;;
        *)
          echo "usage: tools/dev/check.sh [--all] [--skip restore|build|format|contracts|test|scripts]..." >&2
          exit 2
          ;;
      esac
      shift
      ;;
    *)
      echo "usage: tools/dev/check.sh [--all] [--skip <name>]..." >&2
      exit 2
      ;;
  esac
  shift
done

root=$(git -C "$(dirname "$0")" rev-parse --show-toplevel) || exit 2
cd "$root"

dotnet_cmd=${DOTNET:-}
if [ -z "$dotnet_cmd" ]; then
  if command -v dotnet >/dev/null 2>&1; then
    dotnet_cmd=dotnet
  elif command -v dotnet.exe >/dev/null 2>&1; then
    dotnet_cmd=dotnet.exe
  elif [ -x "/mnt/c/Program Files/dotnet/dotnet.exe" ]; then
    dotnet_cmd="/mnt/c/Program Files/dotnet/dotnet.exe"
  else
    echo "dotnet not found: install the SDK in global.json, or set DOTNET" >&2
    exit 2
  fi
fi
contracts_cmd=${CHECK_CONTRACTS:-tools/ci/check-contracts.sh}

# The test scripts run when the change touches tools/ or .github/.
touches_tools() {
  local changed
  if [ -n "${CHECK_CHANGED_FILES-}" ]; then
    changed=$(cat "$CHECK_CHANGED_FILES")
  else
    changed=$({
      git diff --name-only origin/develop...HEAD 2>/dev/null || true
      git diff --name-only HEAD 2>/dev/null || true
      git ls-files --others --exclude-standard 2>/dev/null || true
    })
  fi
  grep -q -E '^(tools|\.github)/' <<<"$changed"
}

log=$(mktemp)
trap 'rm -f "$log"' EXIT
failed=0

# check <name> <command...>: runs the command, keeps its log, prints a FAIL block when it fails.
check() {
  local name=$1
  shift
  case $skips in *" $name "*) return 0 ;; esac
  if "$@" >"$log" 2>&1; then
    return 0
  fi
  failed=1
  echo "FAIL $name"
  echo "  command: $*"
  # sed -n reads all its input, so a large log never ends the pipe early with SIGPIPE under pipefail.
  tr -d '\r' <"$log" | { grep -E -A15 '^\s*Failed |error [A-Z]+[0-9]+:' || true; } | sed -n '1,15s/^/  /p' >"$log.short"
  if [ ! -s "$log.short" ]; then
    tr -d '\r' <"$log" | tail -15 | sed 's/^/  /' >"$log.short"
  fi
  cat "$log.short"
  rm -f "$log.short"
}

# Prints nothing for a passing test; for a failing one a "Failed <test>" line (the log filter finds it) and its first FAIL or error lines.
run_script_tests() {
  local test status=0 test_log
  test_log=$(mktemp)
  for test in ${CHECK_SCRIPT_TESTS:-tools/ci/*.test.sh tools/dev/*.test.sh}; do
    if ! "$test" >"$test_log" 2>&1; then
      status=1
      echo "Failed $test"
      { grep -E -m3 'FAIL|[Ee]rror' "$test_log" || tail -3 "$test_log"; } | sed 's/^/  /'
    fi
  done
  rm -f "$test_log"
  return $status
}

check restore "$dotnet_cmd" restore --locked-mode
check build "$dotnet_cmd" build -c Release --no-restore -v q -clp:NoSummary -tl:off
check format "$dotnet_cmd" format --verify-no-changes --no-restore
check contracts "$contracts_cmd"
check test "$dotnet_cmd" test -c Release --no-build -v q -tl:off --blame-hang-timeout 5m --blame-hang-dump-type none --logger "console;verbosity=minimal"
if [ "$run_all" -eq 1 ] || touches_tools; then
  check scripts run_script_tests
fi

[ "$failed" -eq 0 ] || exit 1
echo "all checks green"
