#!/usr/bin/env bash
# Tests tools/ci/wait-for-health.sh (#278): the HTTPS /health wait that deploy.yml and azure-power.yml share.
# Stub `curl` and `sleep` commands log their arguments, so no network or real waiting is needed.
# Usage: tools/ci/wait-for-health.test.sh   (exit code 0 = every case passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
script="$here/wait-for-health.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# Stubs. curl fails the first FAILS calls and then answers like `curl --fail`; every call is logged.
mkdir -p "$work/bin"
cat > "$work/bin/curl" <<'EOF'
#!/usr/bin/env bash
echo "curl $*" >> "$CALLS"
count=$(( $(cat "$COUNT" 2>/dev/null || echo 0) + 1 ))
echo "$count" > "$COUNT"
[ "$count" -gt "${FAILS:-0}" ]
EOF
cat > "$work/bin/sleep" <<'EOF'
#!/usr/bin/env bash
echo "sleep $*" >> "$CALLS"
EOF
chmod +x "$work/bin/curl" "$work/bin/sleep"

# run <fails> [args...]: runs the script against the stubs; sets $status, $out and $calls
run() {
  local fails=$1
  shift
  : > "$work/calls"
  rm -f "$work/count"
  out=$(PATH="$work/bin:$PATH" CALLS="$work/calls" COUNT="$work/count" FAILS="$fails" "$script" "$@" 2>&1)
  status=$?
  calls=$(cat "$work/calls")
}

# check <description> <condition...>
check() {
  local description=$1
  shift
  if "$@"; then
    echo "PASS  $description"
  else
    echo "FAIL  $description"
    echo "$out" | sed 's/^/      out: /'
    echo "$calls" | sed 's/^/      call: /'
    failures=$((failures + 1))
  fi
}
count_calls() { grep -c "^$1" <<<"$calls"; }

# 1. An endpoint that answers at once: one curl, no sleep.
run 0 https://example.test
check "an endpoint that answers at once passes" test "$status" -eq 0
check "it asks <url>/health with curl --fail --silent --show-error --max-time 10" \
  grep -qxF 'curl --fail --silent --show-error --max-time 10 https://example.test/health' <<<"$calls"
check "it does not sleep when the first attempt answers" test "$(count_calls sleep)" -eq 0

# 2. Two failures, then an answer: it keeps trying every 10 seconds.
run 2 https://example.test
check "an endpoint that answers on the third attempt passes" test "$status" -eq 0
check "it tries once per attempt until /health answers" test "$(count_calls curl)" -eq 3
check "it waits 10 seconds between attempts" test "$(count_calls 'sleep 10')" -eq 2

# 3. No answer: the 30 attempts deploy.yml made, then its error line.
run 999 https://example.test
check "an endpoint that never answers fails" test "$status" -eq 1
check "it makes 30 attempts, the same 5 minutes as deploy.yml" test "$(count_calls curl)" -eq 30
check "the error names the URL and the 5 minutes" \
  grep -qF '::error::https://example.test/health did not answer over HTTPS within 5 minutes' <<<"$out"

# 4. The usage.
run 0
check "no URL: usage error, exit 2" test "$status" -eq 2
check "no URL: it prints the usage" grep -qF 'usage: tools/ci/wait-for-health.sh <public-url>' <<<"$out"
run 0 https://example.test extra
check "more than one argument: usage error, exit 2" test "$status" -eq 2

exit $((failures > 0))
