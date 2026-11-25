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
cat "$STUB_CHECKS"
EOF
chmod +x "$tmp/gh"

# run <checks json> [pr args...]: runs the script against the stub, sets $out, $err and $code.
run() {
  printf '%s' "$1" >"$tmp/checks.json"
  : >"$tmp/log"
  out=$(GH="$tmp/gh" STUB_CHECKS="$tmp/checks.json" STUB_LOG="$tmp/log" "$script" "${2-42}" 2>"$tmp/err")
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

if [ "$failures" -eq 0 ]; then echo "All wait-ci checks passed."; else echo "$failures check(s) failed."; fi
exit $((failures > 0))
