#!/usr/bin/env bash
# Tests tools/dev/wait-ci.sh with a stubbed gh (issue #377): silent on green, only the bad checks otherwise.
# Usage: tools/dev/wait-ci.test.sh   (from the repo root, or anywhere in the repo; exit 0 = pass)
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2
script=tools/dev/wait-ci.sh
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
failures=0

# Stub gh: --watch prints noise and exits non-zero (as gh does on a failed check); --json prints the fixture.
cat >"$tmp/gh" <<'EOF'
#!/usr/bin/env bash
echo "$*" >>"$STUB_LOG"
if [[ " $* " == *" --watch "* ]]; then
  echo "NOISE a green row that must not be printed"
  exit 1
fi
# A "$STUB_CHECKS.first" file is printed once, by the first --json read (checks not registered yet).
if [ -f "$STUB_CHECKS.first" ]; then
  cat "$STUB_CHECKS.first"
  rm "$STUB_CHECKS.first"
  exit 0
fi
cat "$STUB_CHECKS"
EOF
chmod +x "$tmp/gh"

# Stub jq that fails the way a missing jq does (exit 127); a case puts it first on PATH via RUN_PATH.
mkdir "$tmp/nojq"
printf '#!/usr/bin/env bash\nexit 127\n' >"$tmp/nojq/jq"
chmod +x "$tmp/nojq/jq"

# run <checks json> [pr args...]: runs the script against the stub, sets $out, $err and $code.
# The retry wait is cut to 0 seconds so the tests stay fast.
run() {
  printf '%s' "$1" >"$tmp/checks.json"
  : >"$tmp/log"
  out=$(PATH="${RUN_PATH:-$PATH}" GH="$tmp/gh" STUB_CHECKS="$tmp/checks.json" STUB_LOG="$tmp/log" \
    WAIT_CI_TRIES=3 WAIT_CI_DELAY=0 "$script" "${2-42}" 2>"$tmp/err")
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

green='[{"name":"tdd-check","state":"SUCCESS","link":"https://example.test/1"},
        {"name":"docs","state":"SKIPPED","link":"https://example.test/2"}]'
red='[{"name":"tdd-check","state":"SUCCESS","link":"https://example.test/1"},
      {"name":"security","state":"FAILURE","link":"https://example.test/2"},
      {"name":"CI","state":"PENDING","link":"https://example.test/3"}]'

run "$green"
[ "$out" = "all checks green" ] && [ "$code" -eq 0 ]; expect "all green: prints 'all checks green' and exits 0" $?
grep -q -- "--watch" "$tmp/log" && grep -q -- "-R EDB-Sport/fitlife" "$tmp/log"; expect "all green: waits with --watch and passes -R" $?
case $out in *NOISE*) false ;; esac; expect "all green: the --watch output is discarded" $?

run "$red"
[ "$out" = "$(printf 'security FAILURE https://example.test/2\nCI PENDING https://example.test/3')" ] && [ "$code" -ne 0 ]
expect "red: prints only the non-SUCCESS/SKIPPED checks (name, state, link) and exits non-zero" $?
case $out in *tdd-check*) false ;; *) true ;; esac; expect "red: a green check is not printed" $?

run '[]'
[ "$code" -ne 0 ] && [ "$out" != "all checks green" ]; expect "no checks reported: never a false green" $?

run "$green" ""
[ "$code" -eq 2 ]; expect "no PR number: usage error, exit 2" $?

RUN_PATH="$tmp/nojq:$PATH" run "$red"
[ "$code" -eq 2 ] && [ "$out" != "all checks green" ]; expect "jq missing or failing: exit 2, never a false green" $?

printf '[]' >"$tmp/checks.json.first"
run "$green"
[ "$out" = "all checks green" ] && [ "$code" -eq 0 ]
expect "no checks yet (right after a push): waits and reads again" $?

if [ "$failures" -eq 0 ]; then echo "All wait-ci checks passed."; else echo "$failures check(s) failed."; fi
exit $((failures > 0))
