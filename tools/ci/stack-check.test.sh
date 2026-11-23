#!/usr/bin/env bash
# Tests for tools/ci/stack-check.sh (issue #110).
# Each case builds a throwaway git repo with `develop` and some PR branches, then runs the check
# on one PR head. The branch names stand in for origin/<branch>: the script reads them with
# DEVELOP_REF=develop and BRANCH_PREFIX="" here.
# Usage: tools/ci/stack-check.test.sh   (exit code 0 = all cases pass)
set -uo pipefail

check="$(cd "$(dirname "$0")" && pwd)/stack-check.sh"
failures=0
tmp_root=$(mktemp -d)
trap 'rm -rf "$tmp_root"' EXIT

# fixture: a repo with one commit on develop. Prints its path.
fixture() {
  local repo
  repo=$(mktemp -d "$tmp_root/repo.XXXXXX")
  git -C "$repo" init --quiet --initial-branch=develop
  git -C "$repo" config user.email test@example.invalid
  git -C "$repo" config user.name stack-check-test
  commit "$repo" base
  echo "$repo"
}

# commit <repo> <file>: one commit that adds <file>
commit() {
  echo "$2" > "$1/$2"
  git -C "$1" add "$2"
  git -C "$1" commit --quiet -m "add $2"
}

# run_check <repo> <base branch> <head branch> <PR number> <open PR lines>: prints the output, returns the exit code
run_check() {
  (
    cd "$1" || exit 99
    DEVELOP_REF=develop BRANCH_PREFIX="" \
      BASE_REF="$2" HEAD_SHA="$(git rev-parse "$3")" HEAD_BRANCH="$3" PR_NUMBER="$4" OPEN_PRS="$5" \
      "$check" 2>&1
  )
}

# expect <description> <pass|fail> [text the output must contain] -- <repo> <base> <head> <PR number> <open PRs>
expect() {
  local description=$1 verdict=$2 text=$3 out status
  shift 4
  out=$(run_check "$@")
  status=$?
  if [ "$verdict" = pass ] && [ "$status" -eq 0 ]; then
    echo "PASS  $description"
  elif [ "$verdict" = fail ] && [ "$status" -eq 1 ] && grep -qF -- "$text" <<<"$out"; then
    echo "PASS  $description"
  else
    echo "FAIL  $description: expected $verdict${text:+ with '$text'}, got exit $status"
    sed 's/^/      /' <<<"$out"
    failures=$((failures + 1))
  fi
}

open_prs=$'10 feature/a\n11 feature/b'

# 1. A branch made from an up-to-date develop passes, even with other PRs open
repo=$(fixture)
git -C "$repo" checkout --quiet -b feature/a develop && commit "$repo" a
git -C "$repo" checkout --quiet -b feature/b develop && commit "$repo" b
expect "branch from develop passes" pass "" -- "$repo" develop feature/b 11 "$open_prs"

# 2. A branch built on another open PR's branch, base develop, fails and names the parent
repo=$(fixture)
git -C "$repo" checkout --quiet -b feature/a develop && commit "$repo" a
git -C "$repo" checkout --quiet -b feature/b feature/a && commit "$repo" b
expect "stacked on #10 with base develop fails: merge first" fail "merge #10 first" -- \
  "$repo" develop feature/b 11 "$open_prs"
expect "stacked on #10 with base develop fails: set the base" fail "set the base to feature/a" -- \
  "$repo" develop feature/b 11 "$open_prs"

# 3. The same branch with the parent's branch as the base passes
expect "stacked branch with the parent as base passes" pass "" -- \
  "$repo" feature/a feature/b 11 "$open_prs"

# 4. The parent has merged into develop: its commits are on develop now, so it passes
git -C "$repo" checkout --quiet develop && git -C "$repo" merge --quiet --no-ff feature/a -m "Merge #10"
expect "parent merged into develop passes" pass "" -- "$repo" develop feature/b 11 "$open_prs"

# 5. The PR's own branch in the open list is not its own parent
repo=$(fixture)
git -C "$repo" checkout --quiet -b feature/a develop && commit "$repo" a
expect "a PR is not its own parent" pass "" -- "$repo" develop feature/a 10 "$open_prs"

# 6. A PR with no open PRs around passes
expect "no other open PRs passes" pass "" -- "$repo" develop feature/a 10 "10 feature/a"

# 7. The parent gets another commit after the child branched: the child still fails
repo=$(fixture)
git -C "$repo" checkout --quiet -b feature/a develop && commit "$repo" a
git -C "$repo" checkout --quiet -b feature/b feature/a && commit "$repo" b
git -C "$repo" checkout --quiet feature/a && commit "$repo" a2
expect "stacked on a parent that moved still fails" fail "merge #10 first" -- \
  "$repo" develop feature/b 11 "$open_prs"

# 8. The bottom of the stack passes while its child is open, also after it moved
expect "bottom of a stack passes with its child open" pass "" -- "$repo" develop feature/a 10 "$open_prs"

# 9. A parent that merged develop in after the child branched still fails the child
git -C "$repo" checkout --quiet develop && commit "$repo" fix
git -C "$repo" checkout --quiet feature/a && git -C "$repo" merge --quiet develop -m "Merge develop"
expect "stacked on a parent that merged develop in still fails" fail "merge #10 first" -- \
  "$repo" develop feature/b 11 "$open_prs"

# 10. Two PRs that only share commits already on develop don't flag each other
repo=$(fixture)
git -C "$repo" checkout --quiet -b feature/a develop && commit "$repo" a
git -C "$repo" checkout --quiet -b feature/b develop && commit "$repo" b
git -C "$repo" checkout --quiet develop && commit "$repo" later
git -C "$repo" checkout --quiet feature/a && git -C "$repo" merge --quiet develop -m "Merge develop"
git -C "$repo" checkout --quiet feature/b && git -C "$repo" merge --quiet develop -m "Merge develop"
expect "siblings that both merged develop pass" pass "" -- "$repo" develop feature/b 11 "$open_prs"

# 11. Parent and child both merged the same newer develop commit: they have two merge bases,
#     develop's commit and the parent's own commit. The parent's still counts.
repo=$(fixture)
git -C "$repo" checkout --quiet -b feature/a develop && commit "$repo" a
git -C "$repo" checkout --quiet -b feature/b feature/a && commit "$repo" b
git -C "$repo" checkout --quiet develop && commit "$repo" later
git -C "$repo" checkout --quiet feature/a && git -C "$repo" merge --quiet develop -m "Merge develop"
git -C "$repo" checkout --quiet feature/b && git -C "$repo" merge --quiet develop -m "Merge develop"
expect "stacked, both merged the same develop commit, still fails" fail "merge #10 first" -- \
  "$repo" develop feature/b 11 "$open_prs"

# 12. The lower-numbered PR is built on this one (numbers reversed): this one is the bottom, passes
repo=$(fixture)
git -C "$repo" checkout --quiet -b feature/b develop && commit "$repo" b
git -C "$repo" checkout --quiet -b feature/a feature/b && commit "$repo" a
expect "bottom of a stack with the higher number passes" pass "" -- "$repo" develop feature/b 11 "$open_prs"

echo
if [ "$failures" -eq 0 ]; then
  echo "stack-check behaves as specified."
else
  echo "$failures case(s) failed."
fi
exit "$failures"
