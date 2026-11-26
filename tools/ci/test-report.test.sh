#!/usr/bin/env bash
# Tests for tools/ci/test-report.sh (issue #375): a tools/ci/*.test.sh run prints only its FAIL lines
# and one summary line "N cases, M failed"; VERBOSE=1 prints the PASS lines again. Exit codes stay the same.
# Usage: tools/ci/test-report.test.sh   (VERBOSE=1 for the PASS lines; exit code 0 = every case passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
failures=0
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

# fake <name> <body>: a *.test.sh in $tmp that sources the real helper, then runs <body>.
fake() {
  printf '#!/usr/bin/env bash\n. "%s/test-report.sh"\n%s\n' "$here" "$2" >"$tmp/$1"
  chmod +x "$tmp/$1"
}

# run [VAR=value...] <name>: runs a fake as the ci-scripts job does, outside any other test. Sets out and status.
run() {
  out=$(env -u TEST_REPORT_RAW "$@" 2>&1)
  status=$?
}

check() { # check <description> <command...>
  if "${@:2}"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1"
    sed 's/^/      got: /' <<<"$out"
    failures=$((failures + 1))
  fi
}

has() { grep -qxF -- "$1" <<<"$out"; }
lacks() { ! grep -qF -- "$1" <<<"$out"; }

fake green.test.sh 'echo "PASS  one"; echo "PASS  two"; echo; echo "All cases pass."'
fake red.test.sh 'echo "PASS  one"; echo "FAIL  two: expected exit 0, got exit 1"; echo "  detail of two"; echo "PASS  three"; exit 1'
fake code.test.sh 'echo "FAIL  one"; exit 3'
fake crash.test.sh 'echo "PASS  one"; exit 2'
fake nested.test.sh "out=\$('$tmp/green.test.sh'); echo \"\$out\" | sed 's/^/inner: /'"

run "$tmp/green.test.sh"
check "a green run prints only the summary" test "$out" = "2 cases, 0 failed"
check "a green run exits 0" test "$status" = 0

run "$tmp/red.test.sh"
check "a red run hides the PASS lines" lacks "PASS"
check "a red run prints the FAIL line with its expected and actual" has "FAIL  two: expected exit 0, got exit 1"
check "a red run prints the detail lines under a FAIL line" has "  detail of two"
check "a red run ends with one summary line" test "$(tail -1 <<<"$out")" = "3 cases, 1 failed"
check "a red run exits non-zero" test "$status" = 1

run VERBOSE=1 "$tmp/red.test.sh"
check "VERBOSE=1 prints the PASS lines" has "PASS  one"
check "VERBOSE=1 prints the FAIL lines" has "FAIL  two: expected exit 0, got exit 1"
check "VERBOSE=1 ends with the same summary line" test "$(tail -1 <<<"$out")" = "3 cases, 1 failed"

run "$tmp/code.test.sh"
check "the script's own exit code is kept" test "$status" = 3

run "$tmp/crash.test.sh"
check "a script that stops non-zero without a FAIL line says so" has "FAIL  crash.test.sh stopped with exit 2 before any case failed"
check "a script that stops non-zero counts as one failed case" test "$(tail -1 <<<"$out")" = "2 cases, 1 failed"
check "a script that stops non-zero keeps its exit code" test "$status" = 2

run "$tmp/nested.test.sh"
check "a *.test.sh run by another test prints raw lines, for that test to check" has "inner: PASS  one"
check "a *.test.sh run by another test has no summary line of its own" lacks "inner: 2 cases"

# The change is one shared helper: every tools/ci/*.test.sh sources it before it prints anything.
for test in "$here"/*.test.sh; do
  out="no such line in lines 2-12 of $test"
  check "$(basename "$test") sources test-report.sh" \
    grep -qxF '. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"' <(sed -n '2,12p' "$test")
done

exit $((failures > 0))
