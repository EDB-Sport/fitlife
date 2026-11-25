#!/usr/bin/env bash
# Waits for a PR's checks and prints almost nothing when they are green (issue #377).
# Usage: tools/dev/wait-ci.sh <pr>
#   all checks SUCCESS or SKIPPED: prints "all checks green", exit 0
#   otherwise: prints only the other checks as "name state link", exit 1
#   cannot read the checks, or none are reported: message on stderr, exit 2
# gh is found as `gh`, or `gh.exe` on WSL; set GH to override (the test stubs it).
set -uo pipefail

if [ $# -ne 1 ] || [[ ! $1 =~ ^[0-9]+$ ]]; then
  echo "usage: tools/dev/wait-ci.sh <pr-number>" >&2
  exit 2
fi
pr=$1
repo=EDB-Sport/fitlife
gh_cmd=${GH:-$(command -v gh || command -v gh.exe)}

# Wait for the checks; gh --watch prints every row, so its output is discarded.
"$gh_cmd" pr checks "$pr" -R "$repo" --watch >/dev/null 2>&1

checks=$("$gh_cmd" pr checks "$pr" -R "$repo" --json name,state,link | tr -d '\r') || {
  echo "could not read the checks of PR $pr" >&2
  exit 2
}
if [ "$(jq 'length' <<<"$checks")" -eq 0 ]; then
  echo "no checks reported for PR $pr" >&2
  exit 2
fi

bad=$(jq -r '.[] | select(.state != "SUCCESS" and .state != "SKIPPED") | "\(.name) \(.state) \(.link)"' <<<"$checks")
if [ -z "$bad" ]; then
  echo "all checks green"
  exit 0
fi
echo "$bad"
exit 1
