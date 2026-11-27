#!/usr/bin/env bash
# Tests tools/dev/check.sh with a stubbed dotnet (issue #603): silent on green, one FAIL block per failed check.
# Usage: tools/dev/check.test.sh   (from the repo root, or anywhere in the repo; exit 0 = pass)
# Never calls the real dotnet: DOTNET, CHECK_CONTRACTS and CHECK_SCRIPT_TESTS point at stubs.
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2
script=tools/dev/check.sh
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
failures=0
mkdir "$tmp/d" # the stubs read their output and failure markers here; each case sets and clears its own

# Stub dotnet: logs its arguments, prints "$STUB_DIR/<verb>.out" if there is one and fails if "$STUB_DIR/<verb>.fail" exists.
cat >"$tmp/dotnet" <<'STUB'
#!/usr/bin/env bash
echo "dotnet $*" >>"$STUB_LOG"
[ ! -f "$STUB_DIR/$1.out" ] || cat "$STUB_DIR/$1.out"
[ ! -f "$STUB_DIR/$1.fail" ]
STUB
# Stub for check-contracts.sh and for a script test: the same rules, keyed by the stub's own file name.
cat >"$tmp/stub" <<'STUB'
#!/usr/bin/env bash
name=$(basename "$0")
echo "$name $*" >>"$STUB_LOG"
[ ! -f "$STUB_DIR/$name.out" ] || cat "$STUB_DIR/$name.out"
[ ! -f "$STUB_DIR/$name.fail" ]
STUB
cp "$tmp/stub" "$tmp/contracts"
cp "$tmp/stub" "$tmp/scripts.test.sh"
chmod +x "$tmp/dotnet" "$tmp/stub" "$tmp/contracts" "$tmp/scripts.test.sh"

# run [changed file...] -- [check.sh args...]: runs the script against the stubs, sets $out, $err and $code.
# The changed files stand in for the git diff against origin/develop.
run() {
  : >"$tmp/log"
  : >"$tmp/changed"
  while [ $# -gt 0 ] && [ "$1" != "--" ]; do
    echo "$1" >>"$tmp/changed"
    shift
  done
  shift
  out=$(DOTNET="$tmp/dotnet" CHECK_CONTRACTS="$tmp/contracts" CHECK_SCRIPT_TESTS="$tmp/scripts.test.sh" \
    CHECK_CHANGED_FILES="$tmp/changed" STUB_DIR="$tmp/d" STUB_LOG="$tmp/log" "$script" "$@" 2>"$tmp/err")
  code=$?
}

expect() { # expect <label> <condition exit status>
  if [ "$2" -eq 0 ]; then
    echo "PASS  $1"
  else
    echo "FAIL  $1"
    echo "      stdout: $out"
    echo "      stderr: $(cat "$tmp/err")"
    failures=$((failures + 1))
  fi
}

ran() { grep -q -- "$1" "$tmp/log"; }

run src/Booking/Foo.cs --
[ "$out" = "all checks green" ] && [ "$code" -eq 0 ]
# The status of the test above is what expect checks, as on every other expect line.
# shellcheck disable=SC2319
expect "all green: prints only 'all checks green' and exits 0" $?
[ ! -s "$tmp/err" ]; expect "all green: nothing on stderr" $?
order=$(awk '$1 == "dotnet" { print $2; next } { print $1 }' "$tmp/log" | tr '\n' ' ')
[ "$order" = "restore build format contracts test " ]; expect "runs restore, build, format, contracts, test in that order (got: $order)" $?
ran "dotnet restore --locked-mode" && ran "dotnet build -c Release" && ran "dotnet format --verify-no-changes --no-restore" && ran "dotnet test -c Release --no-build"
expect "passes the CI flags: locked restore, Release build, verify-only format, test --no-build" $?
! ran "scripts.test.sh"; expect "a change outside tools/ and .github/ does not run the *.test.sh" $?

run src/Booking/Foo.cs -- --skip build
[ "$code" -eq 0 ] && ! ran "dotnet build"; expect "--skip build: the build does not run and the rest is green" $?

{
  echo "Build noise that is not an error"
  for i in $(seq 1 30); do echo "Foo$i.cs($i,1): error CS1002: ; expected"; done
} >"$tmp/d/build.out"
touch "$tmp/d/build.fail"
run src/Booking/Foo.cs --
fail_blocks=$(printf '%s\n' "$out" | grep -c '^FAIL ')
[ "$code" -eq 1 ] && [ "$fail_blocks" -eq 1 ] && printf '%s\n' "$out" | grep -q '^FAIL build'
expect "one failing check: exit 1 and one FAIL block that names it" $?
printf '%s\n' "$out" | grep -q 'dotnet build -c Release'; expect "the FAIL block shows the command" $?
printf '%s\n' "$out" | grep -q 'Foo1.cs(1,1): error CS1002'; expect "the FAIL block shows the first error lines" $?
[ "$(printf '%s\n' "$out" | grep -c 'error CS1002')" -le 15 ]; expect "the FAIL block shows at most 15 log lines" $?
case $out in *"Build noise"*) false ;; *) true ;; esac; expect "lines that are not errors are filtered out" $?
case $out in *"all checks green"*) false ;; *) true ;; esac; expect "a failure never prints 'all checks green'" $?
ran "dotnet format" && ran "contracts" && ran "dotnet test"; expect "later checks still run after a failure" $?

printf 'Failed Foo.Tests.Bar [5 ms]\n  Error Message: boom\n' >"$tmp/d/test.out"
touch "$tmp/d/test.fail"
rm -f "$tmp/d/build.fail" "$tmp/d/build.out"
run src/Booking/Foo.cs --
printf '%s\n' "$out" | grep -q '^FAIL test' && printf '%s\n' "$out" | grep -q 'Failed Foo.Tests.Bar'
expect "a failing test shows its 'Failed ' line" $?

echo "no error pattern at all" >"$tmp/d/format.out"
touch "$tmp/d/format.fail"
rm -f "$tmp/d/test.fail" "$tmp/d/test.out"
run src/Booking/Foo.cs --
printf '%s\n' "$out" | grep -q '^FAIL format' && printf '%s\n' "$out" | grep -q 'no error pattern at all'
expect "a failure with no recognised error line shows the end of the log instead of nothing" $?

touch "$tmp/d/contracts.fail"
rm -f "$tmp/d/format.fail" "$tmp/d/format.out"
run src/Booking/Foo.cs --
[ "$code" -eq 1 ] && printf '%s\n' "$out" | grep -q '^FAIL contracts'; expect "a failing check-contracts.sh is a FAIL block named contracts" $?

rm -f "$tmp/d/contracts.fail"
run tools/dev/wait-ci.sh --
ran "scripts.test.sh"; expect "a change under tools/ runs the *.test.sh" $?
run .github/workflows/ci.yml --
ran "scripts.test.sh"; expect "a change under .github/ runs the *.test.sh" $?
run src/Booking/Foo.cs -- --all
ran "scripts.test.sh"; expect "--all runs the *.test.sh whatever changed" $?

touch "$tmp/d/scripts.test.sh.fail"
run tools/dev/wait-ci.sh --
[ "$code" -eq 1 ] && printf '%s\n' "$out" | grep -q '^FAIL scripts'; expect "a failing *.test.sh is a FAIL block named scripts" $?

run src/Booking/Foo.cs -- --skip scripts --all
! ran "scripts.test.sh" && [ "$code" -eq 0 ]; expect "--skip scripts beats --all" $?

run src/Booking/Foo.cs -- --skip nonsense
[ "$code" -eq 2 ] && [ -s "$tmp/err" ] && [ -z "$out" ]; expect "an unknown --skip name: usage error on stderr, exit 2, nothing run" $?

if [ "$failures" -eq 0 ]; then echo "All check checks passed."; else echo "$failures check(s) failed."; fi
exit $((failures > 0))
