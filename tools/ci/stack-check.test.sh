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

# 12b. Numbers reversed, then develop merged into the bottom PR (issue #140, like #55 under #32): the
#      bottom's head is no longer an ancestor of the other tip, but its own commits are all in it.
repo=$(fixture)
git -C "$repo" checkout --quiet -b feature/b develop && commit "$repo" b
git -C "$repo" checkout --quiet -b feature/a feature/b && commit "$repo" a
git -C "$repo" checkout --quiet develop && commit "$repo" later
git -C "$repo" checkout --quiet feature/b && git -C "$repo" merge --quiet develop -m "Merge develop"
expect "bottom of a reversed stack passes after develop is merged into it" pass "" -- \
  "$repo" develop feature/b 11 "$open_prs"

# expect_without <description> <text the output must not contain> -- <repo> <base> <head> <PR number> <open PRs>
# The check must still fail (exit 1), but without <text> in its output (issue #116).
expect_without() {
  local description=$1 text=$2 out status
  shift 3
  out=$(run_check "$@")
  status=$?
  if [ "$status" -eq 1 ] && ! grep -qF -- "$text" <<<"$out"; then
    echo "PASS  $description"
  else
    echo "FAIL  $description: expected fail without '$text', got exit $status"
    sed 's/^/      /' <<<"$out"
    failures=$((failures + 1))
  fi
}

# 13. Siblings on one parent (issue #116, like #103 and #105 on #73): #10 and #11 are both built on #9.
#     #11 fails for #9, its parent, with base advice. It also shares #9's commits with #10, but #10 is
#     a sibling, not its parent: the check names #10 without telling the author to set the base to it.
repo=$(fixture)
git -C "$repo" checkout --quiet -b feature/c develop && commit "$repo" c
git -C "$repo" checkout --quiet -b feature/a feature/c && commit "$repo" a
git -C "$repo" checkout --quiet -b feature/b feature/c && commit "$repo" b
sibling_prs=$'9 feature/c\n10 feature/a\n11 feature/b'
expect "sibling stack: the parent still gets base advice" fail "set the base to feature/c" -- \
  "$repo" develop feature/b 11 "$sibling_prs"
expect "sibling stack: the sibling is named" fail "shares unmerged commits with #10" -- \
  "$repo" develop feature/b 11 "$sibling_prs"
expect_without "sibling stack: no base advice for the sibling" "set the base to feature/a" -- \
  "$repo" develop feature/b 11 "$sibling_prs"

# 14. The same siblings after the parent moved on (PR #120 review, reason 1). The graph alone can't
#     tell #9 from #10, so the lower number is the source, as for parents: #9 keeps its base advice.
git -C "$repo" checkout --quiet feature/c && commit "$repo" c2
expect "sibling stack, parent moved: the parent still gets base advice" fail "set the base to feature/c" -- \
  "$repo" develop feature/b 11 "$sibling_prs"
expect "sibling stack, parent moved: the sibling is named" fail "shares unmerged commits with #10" -- \
  "$repo" develop feature/b 11 "$sibling_prs"
expect_without "sibling stack, parent moved: no base advice for the sibling" "set the base to feature/a" -- \
  "$repo" develop feature/b 11 "$sibling_prs"

# 15. A PR stacked on the checked one doesn't make its moved parent a sibling (PR #120 review, reason 2):
#     #12 is built on #11, which is built on #9, and #9 moved on.
repo=$(fixture)
git -C "$repo" checkout --quiet -b feature/c develop && commit "$repo" c
git -C "$repo" checkout --quiet -b feature/b feature/c && commit "$repo" b
git -C "$repo" checkout --quiet -b feature/d feature/b && commit "$repo" d
git -C "$repo" checkout --quiet feature/c && commit "$repo" c2
expect "child of this PR open, parent moved: the parent still gets base advice" fail "set the base to feature/c" -- \
  "$repo" develop feature/b 11 $'9 feature/c\n11 feature/b\n12 feature/d'

echo
if [ "$failures" -eq 0 ]; then
  echo "stack-check behaves as specified."
else
  echo "$failures case(s) failed."
fi
exit "$failures"
