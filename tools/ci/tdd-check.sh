#!/usr/bin/env bash
# tdd-check: checks mechanically that a PR's tests depend on its change (docs/CSHARP_SETUP_TODO.md section 6).
#
# A feature PR (the default):
#   1. Production code changed  -> test code changed too.
#   2. No added skip markers    (NUnit [Ignore], [Explicit], IgnoreReason, Assert.Ignore, Assert.Inconclusive, Assume.).
#   3. No test removed          (the number of [Test]/[TestCase]/... attributes, outside comment lines, does not go down).
#   4. Red on base              the PR's tests pass with the change and fail without it
#                               (production code reset to the base branch, tests kept).
#                               A test that no longer compiles on base counts as red. A run that stops before
#                               any test is built (no project found, restore error: SETUP_ERRORS) does not.
#
# A refactoring PR (label "refactor" in PR_LABELS, METHODOLOGY section 8) changes structure, not behaviour:
#   R1. No file under TEST_DIR changes (added, modified, deleted or moved).
#   R2. The tests pass.
#
# What it does not prove:
#   - The order of work. A test written after the code also fails on base, so the check proves that the
#     tests depend on the change, not that they came first. The optional red:/green: commits
#     (METHODOLOGY section 2) are the evidence of order.
#   - That every change is tested. Only the suite's exit code on base is checked, so one red test lets any
#     amount of untested production code through. And a compile error counts as red, so a PR that adds a
#     new project under src/ always passes check 4. Stryker (section 6) is meant to cover weak tests.
#   - Merging several [TestCase] lines into one [TestCaseSource] lowers the count, so check 3 fails it.
#
#        SRC_DIR may list several production directories, separated by spaces (src/ libs/).
# Usage: BASE_SHA=<base commit> [PR_LABELS=<label,label>] tools/ci/tdd-check.sh
#        On a label event also EVENT_ACTION, EVENT_LABEL, PR_HEAD_SHA, GITHUB_REPOSITORY and GH_TOKEN (issue #136).
#        (run from the repo root; compares BASE_SHA...HEAD)
set -uo pipefail

: "${BASE_SHA:?Set BASE_SHA to the base branch commit}"
HEAD_SHA="${HEAD_SHA:-HEAD}"
TEST_CMD="${TEST_CMD:-dotnet test -c Release}"
# SRC_DIR is one directory or several, separated by spaces (src/ libs/): all of them are production code.
SRC_DIR="${SRC_DIR:-src/}"
read -r -a src_dirs <<< "$SRC_DIR"
TEST_DIR="${TEST_DIR:-tests/}"
SRC_FILE_REGEX="${SRC_FILE_REGEX:-\.cs$}"
TEST_FILE_REGEX="${TEST_FILE_REGEX:-\.cs$}"
# Errors that stop the test run before anything is built: no project or solution found, or a failed restore.
default_setup_errors='error (MSB1003|MSB1009|NU1[0-9]{3}):'
SETUP_ERRORS="${SETUP_ERRORS:-$default_setup_errors}"
SKIP_MARKERS="${SKIP_MARKERS:-[[,][[:space:]]*(Ignore|Explicit)\b|\bIgnoreReason\b|Assert\.(Ignore|Inconclusive)|\bAssume\.}"
TEST_MARKERS="${TEST_MARKERS:-[[,][[:space:]]*(Test|TestCase|TestCaseSource|Theory)\b}"
PR_LABELS="${PR_LABELS:-}"

failed=0
# The failure is an annotation and the first line of the job summary, ahead of any compiler output (#133)
fail() {
  echo "::error::tdd-check: $1"
  failed=1
  if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then echo "- **tdd-check:** $1" >> "$GITHUB_STEP_SUMMARY"; fi
}

# A label event other than "refactor" (issue #136). Labels don't change what tdd-check checks, except refactor,
# so the result of the last completed tdd-check run on the PR's head commit stands: report it again instead of
# running the suite twice more. The check still reports on this run (a skipped required check counts as passing),
# and a label can't turn a failing check green. Without an earlier result, or while another tdd-check run on this
# commit is still going (it may decide the result), run in full.
EVENT_ACTION="${EVENT_ACTION:-}"
EVENT_LABEL="${EVENT_LABEL:-}"
if [[ $EVENT_ACTION == labeled || $EVENT_ACTION == unlabeled ]] && [[ $EVENT_LABEL != refactor ]]; then
  : "${PR_HEAD_SHA:?Set PR_HEAD_SHA to the head commit of the PR}" "${GITHUB_REPOSITORY:?Set GITHUB_REPOSITORY to owner/repo}"
  runs=$(gh api "repos/$GITHUB_REPOSITORY/commits/$PR_HEAD_SHA/check-runs?check_name=${CHECK_NAME:-tdd-check}&filter=all" 2>/dev/null || true)
  # This run itself is one of the runs still going
  going=$(jq '[.check_runs[]? | select(.status != "completed")] | length' <<<"$runs" 2>/dev/null || echo 0)
  last=$(jq -r '[.check_runs[]? | select(.status == "completed" and (.conclusion == "success" or .conclusion == "failure"))]
               | sort_by(.completed_at) | last | .conclusion // ""' <<<"$runs" 2>/dev/null || true)
  if (( going > 1 )); then
    echo "Label \"$EVENT_LABEL\" $EVENT_ACTION, but another tdd-check run on this commit is still going: running in full."
  elif [[ $last == success ]]; then
    echo "Label \"$EVENT_LABEL\" $EVENT_ACTION: it doesn't change tdd-check, so this run reports the last tdd-check result on this commit again: success."
    exit 0
  elif [[ $last == failure ]]; then
    fail "the last tdd-check run on this commit failed, and label \"$EVENT_LABEL\" doesn't change that. See that run for the reason."
    exit 1
  else
    echo "Label \"$EVENT_LABEL\" $EVENT_ACTION, but there is no earlier tdd-check result on this commit: running in full."
  fi
fi

# Files added or modified by the PR under directories, filtered by a regex: changed_files <regex> <dir>... Renames count as delete + add.
changed_files() { git diff --name-only --no-renames --diff-filter=AM "$BASE_SHA...$HEAD_SHA" -- "${@:2}" | grep -E "$1" || true; }

# Number of test attributes in the test files of a commit. Comment lines (// ...) don't count.
count_tests() {
  git ls-tree -r --name-only "$1" -- "$TEST_DIR" | grep -E "$TEST_FILE_REGEX" | while read -r file; do
    git show "$1:$file" | grep -Ev '^[[:space:]]*//' | grep -Eo "$TEST_MARKERS" || true
  done | wc -l | tr -d ' '
}

# Refactoring PR: tests unchanged and green, nothing else.
if [[ ",$PR_LABELS," == *",refactor,"* ]]; then
  echo "Refactoring PR (label \"refactor\"): the tests under $TEST_DIR must not change and must pass."
  touched_tests=$(git diff --name-only "$BASE_SHA...$HEAD_SHA" -- "$TEST_DIR")
  if [[ -n "$touched_tests" ]]; then
    fail "the PR has the refactor label but changes files under $TEST_DIR (METHODOLOGY section 8):"; echo "$touched_tests"
  fi
  echo "::group::Tests (must pass)"
  $TEST_CMD; green=$?
  echo "::endgroup::"
  (( green == 0 )) || fail "the tests fail, so the refactoring changes behaviour."
  if (( failed == 0 )); then echo "tdd-check passed."; fi
  exit "$failed"
fi

src_changes=$(changed_files "$SRC_FILE_REGEX" "${src_dirs[@]}")
test_changes=$(changed_files "$TEST_FILE_REGEX" "$TEST_DIR")

echo "Production files changed:"; echo "${src_changes:-  (none)}"
echo "Test files changed:";       echo "${test_changes:-  (none)}"

# 1. Production code needs tests.
if [[ -n "$src_changes" && -z "$test_changes" ]]; then
  fail "production code under $SRC_DIR changed, but no test under $TEST_DIR was added or changed."
fi

# 2. No new skip markers, in test files only (a README may describe [Ignore] without using it).
skipped=""
if [[ -n "$test_changes" ]]; then
  test_files=()
  while IFS= read -r file; do test_files+=("$file"); done <<< "$test_changes"
  skipped=$(git diff -U0 --no-renames "$BASE_SHA...$HEAD_SHA" -- "${test_files[@]}" | grep -E '^\+[^+]' | grep -E "$SKIP_MARKERS" || true)
fi
if [[ -n "$skipped" ]]; then
  fail "the PR adds skipped tests:"; echo "$skipped"
fi

# 3. No test removed.
base_tests=$(count_tests "$BASE_SHA")
head_tests=$(count_tests "$HEAD_SHA")
echo "Tests on base: $base_tests, tests in the PR: $head_tests"
if (( head_tests < base_tests )); then
  fail "the PR removes tests ($base_tests on base, $head_tests in the PR)."
fi

# 4. Green with the change, red without it. Only meaningful when production code changed.
if [[ -n "$src_changes" ]]; then
  echo "::group::Tests with the change (must pass)"
  $TEST_CMD; green=$?
  echo "::endgroup::"
  (( green == 0 )) || fail "the tests fail with the change."

  # Fail closed: if the base copy can't be set up, red on base is not proven, so the check fails.
  # Only the test run itself may fail (including a compile error on base), and that counts as red.
  worktree=$(mktemp -d)
  if git worktree add --quiet --detach "$worktree" "$HEAD_SHA"; then
    (
      cd "$worktree" || exit 1
      # Every production directory goes back to the base branch together (src/ and libs/), so a test that
      # depends on a change in any of them is red. Tests and everything else stay as in the PR.
      for dir in "${src_dirs[@]}"; do
        rm -rf "$dir"
        if git cat-file -e "$BASE_SHA:$dir" 2>/dev/null; then git checkout --quiet "$BASE_SHA" -- "$dir" || exit 1; fi
      done
      echo "::group::Tests without the change (must fail)"
      # This run is meant to fail, often to compile. Its output is indented, so setup-dotnet's csc problem
      # matcher (lines that start with a file name) doesn't make its errors annotations (#133). The file keeps
      # the raw output for the setup-error check below.
      $TEST_CMD 2>&1 | tee "$worktree/.tdd-check-output" | sed 's/^/  | /'; echo "${PIPESTATUS[0]}" > "$worktree/.tdd-check-exit"
      echo "::endgroup::"
    )
    red=$(cat "$worktree/.tdd-check-exit" 2>/dev/null || echo missing)
    setup_error=$(grep -Eo "$SETUP_ERRORS" "$worktree/.tdd-check-output" 2>/dev/null | head -1 || true)
    git worktree remove --force "$worktree"
    if [[ "$red" == missing ]]; then
      fail "could not prepare the base copy of the code, so red on base is not proven."
    elif [[ -n "$setup_error" ]]; then
      fail "the run without the change failed before any test ran ($setup_error), so red on base is not proven. Check SRC_DIR, TEST_DIR and TEST_CMD."
    elif (( red == 0 )); then
      fail "the PR's tests also pass without the production change, so they don't test it."
    fi
  else
    fail "could not create a worktree for the red-on-base run, so red on base is not proven."
  fi
fi

if (( failed == 0 )); then echo "tdd-check passed."; fi
exit "$failed"
