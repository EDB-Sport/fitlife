#!/usr/bin/env bash
# Waits for a PR's checks and prints almost nothing when they are green (issue #377).
# Usage: tools/dev/wait-ci.sh <pr>
#   all checks SUCCESS or SKIPPED: prints "all checks green", exit 0
#   otherwise: prints only the other checks as "name state link", exit 1
#   cannot read the checks, jq fails, or still no checks after the retries: message on stderr, exit 2
# Right after a push GitHub may not have registered any check yet: the script then waits
# WAIT_CI_DELAY seconds (default 20) and tries again, WAIT_CI_TRIES times in all (default 15). The same when a check
# is still QUEUED, PENDING or IN_PROGRESS after the watch (#633); on the last try such a check is printed as not green.
# gh is found as `gh`, or `gh.exe` on WSL; set GH to override (the test stubs it). Needs jq.
set -uo pipefail

if [ $# -ne 1 ] || [[ ! $1 =~ ^[0-9]+$ ]]; then
  echo "usage: tools/dev/wait-ci.sh <pr-number>" >&2
  exit 2
fi
pr=$1
repo=EDB-Sport/fitlife
gh_cmd=${GH:-$(command -v gh || command -v gh.exe)}
tries=${WAIT_CI_TRIES:-15}
delay=${WAIT_CI_DELAY:-20}
err_file=$(mktemp)
trap 'rm -f "$err_file"' EXIT

for ((try = 1; ; try++)); do
  # Wait for the checks; gh --watch prints every row, so its output is discarded.
  "$gh_cmd" pr checks "$pr" -R "$repo" --watch >/dev/null 2>&1

  # Before any check is registered, gh prints "no checks reported" on stderr and exits 1: that means wait.
  if ! checks=$("$gh_cmd" pr checks "$pr" -R "$repo" --json name,state,link 2>"$err_file" | tr -d '\r'); then
    if ! grep -q "no checks reported" "$err_file"; then
      cat "$err_file" >&2
      echo "could not read the checks of PR $pr" >&2
      exit 2
    fi
    checks='[]'
  fi
  count=$(jq 'length' <<<"$checks") || {
    echo "jq failed (is it installed?): cannot judge the checks of PR $pr" >&2
    exit 2
  }
  # A --watch that ran before any check was registered returns at once, so a check can still be waiting (#633):
  # it is not a result yet, so watch again. On the last try it is printed like any other check that isn't green.
  waiting=$(jq '[.[] | select(.state == "QUEUED" or .state == "PENDING" or .state == "IN_PROGRESS")] | length' <<<"$checks") || {
    echo "jq failed: cannot judge the checks of PR $pr" >&2
    exit 2
  }
  if [ "$count" -gt 0 ] && { [ "$waiting" -eq 0 ] || [ "$try" -ge "$tries" ]; }; then
    break
  fi
  if [ "$try" -ge "$tries" ]; then
    echo "no checks reported for PR $pr after $tries tries" >&2
    exit 2
  fi
  sleep "$delay"
done

bad=$(jq -r '.[] | select(.state != "SUCCESS" and .state != "SKIPPED") | "\(.name) \(.state) \(.link)"' <<<"$checks") || {
  echo "jq failed: cannot judge the checks of PR $pr" >&2
  exit 2
}
if [ -z "$bad" ]; then
  echo "all checks green"
  exit 0
fi
echo "$bad"
exit 1
