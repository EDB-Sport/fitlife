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
  out=$(env -u TEST_REPORT_RAW -u VERBOSE "$@" 2>&1)
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

fake green.test.sh 'echo "PASS  one"; echo; echo "PASS  two"'
fake note.test.sh 'echo "PASS  one"; echo "needs yq"; exit 2'
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

run "$tmp/note.test.sh"
check "a line that is neither PASS nor FAIL is printed, so an error stays visible" has "needs yq"

run "$tmp/code.test.sh"
check "the script's own exit code is kept" test "$status" = 3

run "$tmp/crash.test.sh"
check "a script that stops non-zero without a FAIL line says so" has "FAIL  crash.test.sh stopped with exit 2 before any case failed"
check "a script that stops non-zero counts as one failed case" test "$(tail -1 <<<"$out")" = "2 cases, 1 failed"
check "a script that stops non-zero keeps its exit code" test "$status" = 2

run "$tmp/nested.test.sh"
check "a *.test.sh run by another test prints raw lines, for that test to check" has "inner: PASS  one"
check "a *.test.sh run by another test has no summary line of its own" lacks "inner: 2 cases"

# A FAIL line shows what the case got (issue #477): test_report_got prints the command a failed case ran, with its
# arguments expanded, and test_report_tail the last lines of what it captured.
fake got.test.sh 'out=$(printf "line %s\\n" $(seq 1 25)); if test "$out" = want; then echo "PASS  x"; else echo "FAIL  x"; test_report_got test "the value" = want; test_report_tail out "$out"; exit 1; fi'
run "$tmp/got.test.sh"
check "test_report_got prints the failed command with its arguments expanded" has "      ran: test the\\ value = want"
check "test_report_tail prints the last line the case captured" has "      out: line 25"
check "test_report_tail prints the last 20 lines only" lacks "out: line 5"
check "a case with test_report_got still counts as one failed case" test "$(tail -1 <<<"$out")" = "1 cases, 1 failed"

# planted <script> <helper>: a copy of a tools/ci/*.test.sh with one case that fails on purpose. Its FAIL line has to
# show what the case got, so a red run needs no re-run to see the cause (#477). The case goes above the last line, the exit.
planted() {
  sed -e "s|\$(dirname \"\${BASH_SOURCE\[0\]}\")|$here|; s|\$(dirname \"\$0\")|$here|" \
    -e "\$i\\
$2 \"planted case\" test planted-value = want" "$here/$1" >"$tmp/$1"
  run bash "$tmp/$1"
}
for planted_case in summarize.test.sh:check ci-workflow.test.sh:expect image-scan.test.sh:check; do
  planted "${planted_case%%:*}" "${planted_case##*:}"
  check "${planted_case%%:*}: a failed case shows the command it ran" has "      ran: test planted-value = want"
  check "${planted_case%%:*}: a failed case still fails the run" test "$status" != 0
done

# The change is one shared helper: every tools/ci/*.test.sh sources it before it prints anything.
for test in "$here"/*.test.sh; do
  out="no such line in lines 2-12 of $test"
  check "$(basename "$test") sources test-report.sh" \
    grep -qxF '. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"' <(sed -n '2,12p' "$test")
done

# A real script on a green run (PR #476 review): security-steps.test.sh printed the gitleaks step's output
# and its shell on every run. It needs yq and docker, which the ci-scripts job has.
if command -v yq >/dev/null && timeout 30 docker info >/dev/null 2>&1; then
  run "$here/security-steps.test.sh"
  check "security-steps.test.sh on a green run prints only the summary" \
    bash -c '[[ $1 =~ ^[0-9]+\ cases,\ 0\ failed$ ]]' _ "$out"
else
  echo "skipped: security-steps.test.sh on a green run (needs yq and docker)"
fi

exit $((failures > 0))
