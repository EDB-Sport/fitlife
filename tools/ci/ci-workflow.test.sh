#!/usr/bin/env bash
# Checks that .github/workflows/ci.yml runs the CI scripts that aren't *.test.sh (issue #54),
# and that it never cancels a push run on develop or main (issue #131).
# Usage: tools/ci/ci-workflow.test.sh   (exit code 0 = every check passes)
set -uo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
ci="$root/.github/workflows/ci.yml"
failures=0

# job_block <job name>: the lines of one job, from "  <name>:" to the next job
job_block() {
  awk -v job="  $1:" '
    $0 == job { inside = 1; print; next }
    inside && /^  [A-Za-z0-9_-]+:/ { exit }
    inside { print }
  ' "$ci"
}

# expect <description> <command...>
expect() {
  local description=$1
  shift
  if "$@"; then
    echo "PASS  $description"
  else
    echo "FAIL  $description"
    failures=$((failures + 1))
  fi
}

# One compile error fails build only: format and test wait for it and are skipped (issue #134).
# The job names stay the same, so required checks keep working.
for job in build format test; do
  expect "ci.yml has a $job job" test -n "$(job_block "$job")"
done
for job in format test; do
  expect "$job runs only after build" grep -qE '^    needs: \[?build\]?$' <<<"$(job_block "$job")"
done

build_rules=$(job_block build-rules)

expect "ci.yml runs on pull requests" grep -qE '^  pull_request:' "$ci"
# A newer push cancels a PR's run, but never a push run on develop or main (issue #131):
# close merges would cancel each other's runs and leave a merge without a verdict.
expect "ci.yml cancels in-progress runs for pull requests only" \
  grep -qF "cancel-in-progress: \${{ github.event_name == 'pull_request' }}" "$ci"
expect "ci.yml has a build-rules job" test -n "$build_rules"
expect "build-rules runs tools/ci/check-build-rules.sh" \
  grep -qE 'run: tools/ci/check-build-rules\.sh$' <<<"$build_rules"
expect "build-rules sets up the SDK pinned in global.json" \
  grep -qE 'global-json-file: global\.json' <<<"$build_rules"
expect "build-rules has no continue-on-error" \
  bash -c '! grep -q "continue-on-error" <<<"$1"' _ "$build_rules"

stack_check=$(job_block stack-check)

expect "ci.yml has a stack-check job" test -n "$stack_check"
expect "stack-check runs tools/ci/stack-check.sh" \
  grep -qE 'run: tools/ci/stack-check\.sh$' <<<"$stack_check"
expect "stack-check runs on pull requests only" \
  grep -qF "if: github.event_name == 'pull_request'" <<<"$stack_check"
expect "stack-check checks out the full history" \
  grep -qE 'fetch-depth: 0' <<<"$stack_check"
expect "stack-check can read pull requests" \
  grep -qE 'pull-requests: read' <<<"$stack_check"
expect "stack-check cannot write anything" \
  bash -c '! grep -qE "(contents|issues|pull-requests): write" <<<"$1"' _ "$stack_check"

echo
if [ "$failures" -eq 0 ]; then
  echo "ci.yml runs every CI script."
else
  echo "$failures check(s) failed."
fi
exit "$failures"
