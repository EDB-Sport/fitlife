#!/usr/bin/env bash
# Tests for tools/dev/review-ready.sh (issue #462). A fake gh (passed as GH) answers from JSON
# fixtures in a temp directory and logs its calls, so no case touches GitHub.
# Usage: tools/dev/review-ready.test.sh   (from anywhere in the repo; exit 0 = all cases pass)
set -uo pipefail

root=$(git -C "$(dirname "$0")" rev-parse --show-toplevel) || exit 2
check="$root/tools/dev/review-ready.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

fake="$work/gh"
cat > "$fake" <<'FAKE'
#!/usr/bin/env bash
# Fake gh: answers from the fixtures in $FAKE_GH_DIR, applies --jq as gh does, logs each call.
set -uo pipefail
dir=$FAKE_GH_DIR
echo "$*" >> "$dir/calls"
if [ "${FAKE_GH_FAIL:-}" = "$1 $2" ]; then echo "fake gh: failing on $1 $2" >&2; exit 1; fi
jq_filter=""
prev=""
for arg in "$@"; do
  [ "$prev" = --jq ] && jq_filter=$arg
  prev=$arg
done
case "$*" in
  "pr view "*) src=pr.json ;;
  "pr list "*) src=open-prs.json ;;
  "api repos/EDB-Sport/fitlife/compare/"*) src=compare.json ;;
  "api repos/EDB-Sport/fitlife/rules/branches/"*) src=ruleset.json ;;
  *) echo "fake gh: unexpected call: $*" >&2; exit 9 ;;
esac
if [ -n "$jq_filter" ]; then jq -r "$jq_filter" "$dir/$src"; else cat "$dir/$src"; fi
FAKE
chmod +x "$fake"

failures=0
fx=""

# new_case: a fresh fixture directory holding an all-green PR into develop, with nothing behind develop
new_case() {
  fx=$(mktemp -d "$work/case.XXXXXX")
  cat > "$fx/pr.json" <<'JSON'
{"isDraft":false,"mergeable":"MERGEABLE","mergeStateStatus":"CLEAN","baseRefName":"develop","headRefOid":"abc123","body":"Closes #1",
 "statusCheckRollup":[
  {"__typename":"CheckRun","name":"build","status":"COMPLETED","conclusion":"SUCCESS","detailsUrl":"https://run/build"},
  {"__typename":"CheckRun","name":"test","status":"COMPLETED","conclusion":"SUCCESS","detailsUrl":"https://run/test"},
  {"__typename":"CheckRun","name":"contract","status":"COMPLETED","conclusion":"SKIPPED","detailsUrl":"https://run/contract"},
  {"__typename":"StatusContext","context":"security","state":"SUCCESS","targetUrl":"https://run/security"},
  {"__typename":"CheckRun","name":"mutation","status":"COMPLETED","conclusion":"SUCCESS","detailsUrl":"https://run/mutation"}
 ]}
JSON
  cat > "$fx/ruleset.json" <<'JSON'
[{"type":"pull_request","parameters":{}},
 {"type":"required_status_checks","parameters":{"required_status_checks":[
   {"context":"build"},{"context":"test"},{"context":"contract"},{"context":"security"}]}}]
JSON
  echo '{"ahead_by":0,"behind_by":0,"files":[]}' > "$fx/compare.json"
  echo '[]' > "$fx/open-prs.json"
}

# edit <fixture file> <jq filter>: changes one fixture in the current case
edit() {
  jq "$2" "$fx/$1" > "$fx/tmp.json" && mv "$fx/tmp.json" "$fx/$1"
}

# expect <case> <want exit> <output text, or -> : runs the script on PR 1 against the current fixtures
expect() {
  local name=$1 want=$2 text=$3 out code
  out=$(GH="$fake" FAKE_GH_DIR="$fx" "$check" 1 2>&1)
  code=$?
  if [ "$code" -eq "$want" ] && { [ "$text" = - ] || grep -qF -- "$text" <<<"$out"; }; then
    echo "PASS  $name"
  else
    echo "FAIL  $name (want exit $want with output containing '$text'; got exit $code)"
    sed 's/^/      /' <<<"$out"
    failures=$((failures + 1))
  fi
}

new_case
expect "all green: ready (SKIPPED and a failing non-required check don't block)" 0 "ready for review"
[ "$(grep -c '^pr view ' "$fx/calls")" -eq 1 ] && echo "PASS  one pr view call" || { echo "FAIL  expected one pr view call"; failures=$((failures + 1)); }

new_case; edit pr.json '(.statusCheckRollup[] | select(.name=="mutation") | .conclusion) = "FAILURE"'
expect "failing mutation (not a required check) does not block" 0 "ready for review"

new_case; edit pr.json '.isDraft=true'
expect "draft PR: ready (a draft can be reviewed, #470)" 0 "ready for review"

# The rollup isn't in time order: the run that started last decides, wherever it is listed.
new_case; edit pr.json '.statusCheckRollup |= [{"__typename":"CheckRun","name":"test","status":"COMPLETED","conclusion":"FAILURE","startedAt":"2026-11-26T10:05:00Z","detailsUrl":"https://run/test-new"}] + map(if .name=="test" then .startedAt="2026-11-26T10:00:00Z" else . end)'
expect "newer failing run listed first: not ready" 1 "not ready: required check test is FAILURE on the head: https://run/test-new"

new_case; edit pr.json '.statusCheckRollup |= map(if .name=="test" then .startedAt="2026-11-26T10:00:00Z" else . end) + [{"__typename":"CheckRun","name":"test","status":"COMPLETED","conclusion":"FAILURE","startedAt":"2026-11-26T10:05:00Z","detailsUrl":"https://run/test-new"}]'
expect "newer failing run listed last: not ready" 1 "not ready: required check test is FAILURE on the head: https://run/test-new"

new_case; edit pr.json '.statusCheckRollup |= [{"__typename":"CheckRun","name":"test","status":"COMPLETED","conclusion":"FAILURE","startedAt":"2026-11-26T09:55:00Z","detailsUrl":"https://run/test-old"}] + map(if .name=="test" then .startedAt="2026-11-26T10:00:00Z" else . end)'
expect "older failing run listed first, newer run green: ready" 0 "ready for review"

new_case; edit pr.json '.statusCheckRollup |= map(if .name=="test" then .startedAt="2026-11-26T10:00:00Z" else . end) + [{"__typename":"CheckRun","name":"test","status":"COMPLETED","conclusion":"FAILURE","startedAt":"2026-11-26T09:55:00Z","detailsUrl":"https://run/test-old"}]'
expect "older failing run listed last, newer run green: ready" 0 "ready for review"

new_case; edit pr.json '.statusCheckRollup |= [{"__typename":"CheckRun","name":"test","status":"QUEUED","conclusion":null,"startedAt":"0001-01-01T00:00:00Z","detailsUrl":"https://run/test-rerun"}] + map(if .name=="test" then .startedAt="2026-11-26T10:00:00Z" | .conclusion="FAILURE" else . end)'
expect "re-run queued after a failure (not started yet): the re-run decides" 1 "not ready: required check test is QUEUED on the head: https://run/test-rerun"

new_case; edit pr.json '(.statusCheckRollup[] | select(.name=="test") | .conclusion) = "FAILURE"'
expect "failing required check: named with its run link" 1 "not ready: required check test is FAILURE on the head: https://run/test"

new_case; edit pr.json '.statusCheckRollup |= map(select(.name != "security" and .context != "security"))'
expect "required check with no run on the head: not ready" 1 "not ready: required check security has no run on the head"

new_case; edit pr.json '(.statusCheckRollup[] | select(.name=="build") | .status) = "IN_PROGRESS" | (.statusCheckRollup[] | select(.name=="build") | .conclusion) = null'
expect "required check still running: not ready" 1 "not ready: required check build is IN_PROGRESS on the head"

new_case; edit pr.json '(.statusCheckRollup[] | select(.context=="security") | .state) = "FAILURE"'
expect "failing status context counts as a required check" 1 "not ready: required check security is FAILURE on the head: https://run/security"

new_case; edit pr.json '.mergeable="CONFLICTING"'
expect "conflict with the base: not ready" 1 "not ready: conflicts with develop"

new_case; edit pr.json '.baseRefName="feature/parent"'
expect "stacked on a branch with no open PR: broken stack" 1 "not ready: base feature/parent has no open PR"

new_case; edit pr.json '.baseRefName="feature/parent"'; echo '[{"headRefName":"feature/parent"}]' > "$fx/open-prs.json"
expect "stacked on an open PR's branch: ready" 0 "ready for review"

new_case; edit compare.json '.ahead_by=3 | .files=[{"filename":"src/x.cs"}]'
expect "behind develop, no workflow change: ready, with an info line" 0 "info: 3 commit(s) behind develop"

new_case; edit compare.json '.ahead_by=3 | .files=[{"filename":".github/workflows/ci.yml"},{"filename":"src/x.cs"}]'
expect "workflow changed on develop after the merge base: CI is stale, not ready" 1 "not ready: CI on the head is stale: develop changed .github/workflows/ci.yml"

new_case; edit pr.json '.mergeable="CONFLICTING" | (.statusCheckRollup[] | select(.name=="test") | .conclusion) = "FAILURE"'
out=$(GH="$fake" FAKE_GH_DIR="$fx" "$check" 1 2>&1)
if [ "$(grep -c '^not ready: ' <<<"$out")" -eq 2 ]; then
  echo "PASS  one line per reason (failing check and conflict)"
else
  echo "FAIL  one line per reason: expected 2 'not ready:' lines"
  sed 's/^/      /' <<<"$out"
  failures=$((failures + 1))
fi

new_case
out=$(FAKE_GH_FAIL="pr view" GH="$fake" FAKE_GH_DIR="$fx" "$check" 1 2>&1); code=$?
if [ "$code" -eq 2 ]; then
  echo "PASS  cannot read the PR: exit 2, not 'ready'"
else
  echo "FAIL  cannot read the PR: expected exit 2, got $code"
  sed 's/^/      /' <<<"$out"
  failures=$((failures + 1))
fi

if [ "$failures" -gt 0 ]; then
  echo
  echo "$failures case(s) failed."
  exit 1
fi
echo "All review-ready cases pass."
