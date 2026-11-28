#!/usr/bin/env bash
# Shared report for every tools/ci/*.test.sh (issue #375). Source it first, before the script prints anything:
#   . "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
# It runs the script again with its stdout through a filter, so a run prints only what is wrong:
#   - "PASS  …" lines and blank lines are hidden; VERBOSE=1 prints them again.
#   - "FAIL  …" lines and the lines under them (expected, actual, captured output) are printed.
#   - One summary line ends the run: "N cases, M failed".
# The exit code is the script's own. A script that stops non-zero without a FAIL line gets one, naming the exit code.
# A *.test.sh that another test runs (TEST_REPORT_RAW is set) prints its raw lines, for that test to check.
# Under a FAIL line a case shows what it got (issue #477), with two helpers:
#   test_report_got <command...>       the command the case ran, its arguments expanded (cut at 500 characters)
#   test_report_tail <label> <text>    the last 20 lines of what the case captured, each line starting with "<label>: "

test_report_got() {
  local ran
  ran=$(printf ' %q' "$@")
  echo "      ran:${ran:0:500}"
}

test_report_tail() {
  [ -n "$2" ] || return 0
  tail -n 20 <<<"$2" | sed "s/^/      $1: /"
}

if [ -z "${TEST_REPORT_RAW:-}" ]; then
  export TEST_REPORT_RAW=1
  test_report_counts=$(mktemp)
  "$BASH" "$0" "$@" | awk -v verbose="${VERBOSE:-0}" -v counts="$test_report_counts" '
    /^PASS / { cases++; if (verbose == "1") print; fflush(); next }
    /^FAIL / { cases++; failed++; print; fflush(); next }
    /^[[:space:]]*$/ { if (verbose == "1") print; next }
    { print; fflush() }
    END { print cases + 0, failed + 0 > counts }
  '
  test_report_status=${PIPESTATUS[0]}
  read -r test_report_cases test_report_failed <"$test_report_counts"
  rm -f "$test_report_counts"
  if [ "$test_report_status" != 0 ] && [ "$test_report_failed" = 0 ]; then
    echo "FAIL  $(basename "$0") stopped with exit $test_report_status before any case failed"
    test_report_cases=$((test_report_cases + 1)) test_report_failed=1
  fi
  echo "$test_report_cases cases, $test_report_failed failed"
  exit "$test_report_status"
fi
