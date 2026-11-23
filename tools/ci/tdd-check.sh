#!/usr/bin/env bash
# tdd-check: checks mechanically that a PR was built test-first (docs/CSHARP_SETUP_TODO.md section 6).
#
#   1. Production code changed  -> test code changed too.
#   2. No added skip markers    (NUnit [Ignore], [Explicit], Assert.Ignore, Assert.Inconclusive).
#   3. No test removed          (the number of [Test]/[TestCase]/... attributes does not go down).
#   4. Red on base              the PR's tests pass with the change and fail without it
#                               (production code reset to the base branch, tests kept).
#                               A test that no longer compiles on base counts as red.
#
# Usage: BASE_SHA=<base commit> tools/ci/tdd-check.sh   (run from the repo root; compares BASE_SHA...HEAD)
set -uo pipefail

: "${BASE_SHA:?Set BASE_SHA to the base branch commit}"
HEAD_SHA="${HEAD_SHA:-HEAD}"
TEST_CMD="${TEST_CMD:-dotnet test -c Release}"
SRC_DIR="${SRC_DIR:-src/}"
TEST_DIR="${TEST_DIR:-tests/}"
SRC_FILE_REGEX="${SRC_FILE_REGEX:-\.cs$}"
TEST_FILE_REGEX="${TEST_FILE_REGEX:-\.cs$}"
SKIP_MARKERS="${SKIP_MARKERS:-[[,][[:space:]]*(Ignore|Explicit)\b|Assert\.(Ignore|Inconclusive)}"
TEST_MARKERS="${TEST_MARKERS:-[[,][[:space:]]*(Test|TestCase|TestCaseSource|Theory)\b}"

failed=0
fail() { echo "::error::tdd-check: $1"; failed=1; }

# Files added or modified by the PR under a directory, filtered by a regex. Renames count as delete + add.
changed_files() { git diff --name-only --no-renames --diff-filter=AM "$BASE_SHA...$HEAD_SHA" -- "$1" | grep -E "$2" || true; }

# Number of test attributes in the test files of a commit.
count_tests() {
  git ls-tree -r --name-only "$1" -- "$TEST_DIR" | grep -E "$TEST_FILE_REGEX" | while read -r file; do
    git show "$1:$file" | grep -Eo "$TEST_MARKERS" || true
  done | wc -l | tr -d ' '
}

src_changes=$(changed_files "$SRC_DIR" "$SRC_FILE_REGEX")
test_changes=$(changed_files "$TEST_DIR" "$TEST_FILE_REGEX")

echo "Production files changed:"; echo "${src_changes:-  (none)}"
echo "Test files changed:";       echo "${test_changes:-  (none)}"

# 1. Production code needs tests.
if [[ -n "$src_changes" && -z "$test_changes" ]]; then
  fail "production code under $SRC_DIR changed, but no test under $TEST_DIR was added or changed."
fi

# 2. No new skip markers.
skipped=$(git diff -U0 --no-renames "$BASE_SHA...$HEAD_SHA" -- "$TEST_DIR" | grep -E '^\+[^+]' | grep -E "$SKIP_MARKERS" || true)
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

  worktree=$(mktemp -d)
  git worktree add --quiet --detach "$worktree" "$HEAD_SHA"
  (
    cd "$worktree" || exit 1
    rm -rf "$SRC_DIR"
    # Restore the base branch's production code, if it had any. Tests and everything else stay as in the PR.
    if git cat-file -e "$BASE_SHA:$SRC_DIR" 2>/dev/null; then git checkout --quiet "$BASE_SHA" -- "$SRC_DIR"; fi
    echo "::group::Tests without the change (must fail)"
    $TEST_CMD; echo $? > "$worktree/.tdd-check-exit"
    echo "::endgroup::"
  )
  red=$(cat "$worktree/.tdd-check-exit" 2>/dev/null || echo 1)
  git worktree remove --force "$worktree"
  (( red != 0 )) || fail "the PR's tests also pass without the production change, so they don't test it."
fi

if (( failed == 0 )); then echo "tdd-check passed."; fi
exit "$failed"
