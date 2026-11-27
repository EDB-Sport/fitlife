#!/usr/bin/env bash
# Every workflow that runs on pull_request cancels a PR's superseded run, and never a push run on develop or main
# (issue #602): a superseded run holds one of the 20 runner slots for nothing, and close merges must not cancel
# each other's runs (#131).
#   - a workflow with pull_request and push: a top-level concurrency block whose group has github.event_name, and
#     cancel-in-progress: ${{ github.event_name == 'pull_request' }}
#   - a pull_request-only workflow: a top-level concurrency block whose cancel-in-progress is not false
# Usage: tools/ci/workflow-concurrency.test.sh   (exit 0 = every check passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# Not checked, with the reason:
#   release.yml: its pull_request run is the release gate of a release PR into main, and its push run tags and
#   pushes the images; it is never cancelled (cancel-in-progress: false), and release PRs are rare.
exempt="release.yml"

# top_block <key> <file>: the lines under a top-level key, up to the next top-level key
top_block() {
  awk -v key="$1:" '$0 == key { inside = 1; next } inside && /^[A-Za-z_-]+:/ { exit } inside { print }' "$2"
}

# concurrency_problem <workflow file>: prints what is wrong, or nothing
concurrency_problem() {
  local on concurrency group cancel
  on=$(top_block on "$1")
  grep -qE '^  pull_request:' <<<"$on" || return 0
  concurrency=$(top_block concurrency "$1")
  if [ -z "$concurrency" ]; then
    echo "no top-level concurrency block"
    return
  fi
  group=$(sed -n 's/^  group: //p' <<<"$concurrency")
  cancel=$(sed -n 's/^  cancel-in-progress: //p' <<<"$concurrency")
  if grep -qE '^  push:' <<<"$on"; then
    grep -qF 'github.event_name' <<<"$group" ||
      echo "the group has no github.event_name, so a PR run could cancel a push run"
    [ "$cancel" = "\${{ github.event_name == 'pull_request' }}" ] ||
      echo "cancel-in-progress is '$cancel', not \${{ github.event_name == 'pull_request' }}"
  else
    case "$cancel" in
      "" | false) echo "cancel-in-progress is '$cancel', so a superseded PR run is not cancelled" ;;
    esac
  fi
}

# expect_problem <description> <want: none|some> <workflow file>
expect_problem() {
  local got
  got=$(concurrency_problem "$3")
  if { [ "$2" = none ] && [ -z "$got" ]; } || { [ "$2" = some ] && [ -n "$got" ]; }; then
    echo "PASS  $1"
  else
    echo "FAIL  $1"
    echo "  wanted: $2 problem; got: ${got:-none}"
    failures=$((failures + 1))
  fi
}

# The check itself, on small workflows
push_and_pr='on:
  push:
    branches: [develop, main]
  pull_request:
'
printf '%s\njobs: {}\n' "$push_and_pr" >"$work/none.yml"
expect_problem "a push + pull_request workflow without concurrency is refused" some "$work/none.yml"
printf '%sconcurrency:\n  group: x-${{ github.ref }}\n  cancel-in-progress: true\njobs: {}\n' "$push_and_pr" >"$work/always.yml"
expect_problem "a push + pull_request workflow that cancels every run is refused" some "$work/always.yml"
# shellcheck disable=SC2016 # the workflow text holds ${{ }} literally
printf '%sconcurrency:\n  group: x-${{ github.event_name }}-${{ github.ref }}\n  cancel-in-progress: ${{ github.event_name == '"'pull_request'"' }}\njobs: {}\n' \
  "$push_and_pr" >"$work/ok.yml"
expect_problem "a push + pull_request workflow that cancels PR runs only passes" none "$work/ok.yml"
printf 'on:\n  pull_request:\nconcurrency:\n  group: x\n  cancel-in-progress: false\njobs: {}\n' >"$work/pr-false.yml"
expect_problem "a pull_request-only workflow that never cancels is refused" some "$work/pr-false.yml"
printf 'on:\n  workflow_dispatch:\njobs: {}\n' >"$work/manual.yml"
expect_problem "a workflow without pull_request needs no concurrency" none "$work/manual.yml"

# The repo's workflows
for workflow in "$root"/.github/workflows/*.yml; do
  name=$(basename "$workflow")
  case " $exempt " in *" $name "*) continue ;; esac
  expect_problem "$name cancels superseded PR runs only" none "$workflow"
done

exit $((failures > 0))
