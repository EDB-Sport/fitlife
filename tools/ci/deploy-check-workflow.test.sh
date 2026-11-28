#!/usr/bin/env bash
# Checks how deploy-check.yml is started (issue #283): one run per PR update, and the job keeps the
# name of the required check. "pull_request" checks the PR's own commit; "push" is only the backstop
# for direct pushes and merges to develop and main, so a push to a PR branch starts no second run.
# Usage: tools/ci/deploy-check-workflow.test.sh   (exit 0 = every check passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
workflow="$root/.github/workflows/deploy-check.yml"
failures=0

# on_block: the lines of the top-level "on:" key, up to the next top-level key
on_block() {
  awk '/^on:/ { inside = 1; next } inside && /^[A-Za-z_-]+:/ { exit } inside { print }' "$workflow"
}
# trigger_block <event>: the lines of one event under "on:", up to the next event
trigger_block() {
  on_block | awk -v ev="  $1:" '
    $0 == ev { inside = 1; next }
    inside && /^  [A-Za-z_]+:/ { exit }
    inside { print }'
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

push_block=$(trigger_block push)
expect "push is limited to develop and main, so a PR push starts one run, not two" \
  bash -c 'tr -d " " <<<"$1" | grep -qx "branches:\[develop,main\]"' _ "$push_block"
expect "pull_request is a trigger, so the PR's own commit is checked" \
  grep -qE '^  pull_request:' <<<"$(on_block)"

# The job name is the check name: a "name:" would rename the required check, and the ruleset would
# wait for it forever. The job stays in this workflow until the group decides otherwise (#283).
job_block=$(awk '/^  deploy-check:/ { inside = 1; print; next } inside && /^  [A-Za-z0-9_-]+:/ { exit } inside { print }' "$workflow")
expect "the job is still called deploy-check" test -n "$job_block"
expect "the job sets no name:, which would rename the required check" \
  bash -c '! grep -qE "^    name:" <<<"$1"' _ "$job_block"

# The acceptance scripts under deploy/ guard develop (#483): the job runs deploy-check.sh, which runs every
# deploy/**/*.test.sh it finds, so a new deploy/compose/*.test.sh needs no workflow change.
script="$root/tools/ci/deploy-check.sh"
expect "the job runs tools/ci/deploy-check.sh" grep -qE 'run: .*tools/ci/deploy-check\.sh' <<<"$job_block"
expect "deploy-check.sh finds every deploy/**/*.test.sh, with no list to keep up to date" \
  grep -qF "find deploy -type f -name '*.test.sh'" "$script"
expect "deploy-check.sh runs each acceptance script it found and fails when one fails" \
  bash -c 'grep -qE "^  if BICEP=.* bash \"\\\$check\"; then$" "$1" && grep -qF "fail \"\$check failed" "$1"' _ "$script"
expect "deploy/compose has acceptance scripts for it to run" \
  test -n "$(git -C "$root" ls-files 'deploy/compose/*.test.sh')"

exit $((failures > 0))
