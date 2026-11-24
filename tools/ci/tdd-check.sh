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
# Usage: BASE_SHA=<base commit> [PR_LABELS=<label,label>] tools/ci/tdd-check.sh
#        (run from the repo root; compares BASE_SHA...HEAD)
set -uo pipefail

: "${BASE_SHA:?Set BASE_SHA to the base branch commit}"
HEAD_SHA="${HEAD_SHA:-HEAD}"
TEST_CMD="${TEST_CMD:-dotnet test -c Release}"
SRC_DIR="${SRC_DIR:-src/}"
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
fail() { echo "::error::tdd-check: $1"; failed=1; }

# Files added or modified by the PR under a directory, filtered by a regex. Renames count as delete + add.
changed_files() { git diff --name-only --no-renames --diff-filter=AM "$BASE_SHA...$HEAD_SHA" -- "$1" | grep -E "$2" || true; }

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

src_changes=$(changed_files "$SRC_DIR" "$SRC_FILE_REGEX")
test_changes=$(changed_files "$TEST_DIR" "$TEST_FILE_REGEX")

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
      rm -rf "$SRC_DIR"
      # Restore the base branch's production code, if it had any. Tests and everything else stay as in the PR.
      if git cat-file -e "$BASE_SHA:$SRC_DIR" 2>/dev/null; then git checkout --quiet "$BASE_SHA" -- "$SRC_DIR" || exit 1; fi
      echo "::group::Tests without the change (must fail)"
      $TEST_CMD 2>&1 | tee "$worktree/.tdd-check-output"; echo "${PIPESTATUS[0]}" > "$worktree/.tdd-check-exit"
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
