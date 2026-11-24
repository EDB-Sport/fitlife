#!/usr/bin/env bash
# Fails a pull request that targets develop but is built on another open PR's branch (issue #110).
# Another open PR is a parent when it has a lower number than this PR and shares commits with
# it that aren't on develop (one of their merge bases, `git merge-base --all`, isn't on develop). It doesn't matter whether the
# parent has moved on since: shared commits, not the parent's current tip, are what a reviewer
# sees twice. The lower-number rule keeps the bottom of a stack passing while its child is open.
# If the lower-numbered PR is itself built on this one (numbers reversed), this PR is the bottom
# and passes; the other PR is the one that is stacked, but it is not caught.
# Inputs (environment):
#   BASE_REF     the PR's base branch; only a PR whose base is develop is checked
#   HEAD_SHA     the PR's head commit
#   PR_NUMBER    this PR's number
#   HEAD_BRANCH  the PR's head branch (never its own parent)
#   OPEN_PRS     the open PRs, one "<number> <head branch>" per line; when unset, read with gh
#                (needs GH_TOKEN and GITHUB_REPOSITORY)
#   DEVELOP_REF  default origin/develop
#   BRANCH_PREFIX  default origin/ (where the open PRs' branches are found)
# A PR from a fork has no branch here, so it can be checked but never be a parent.
set -uo pipefail

develop_ref=${DEVELOP_REF:-origin/develop}
prefix=${BRANCH_PREFIX-origin/}

[ "${BASE_REF:-}" = develop ] || { echo "Base is '${BASE_REF:-}', not develop: nothing to check."; exit 0; }

if [ -z "${OPEN_PRS+x}" ]; then
  OPEN_PRS=$(gh pr list -R "$GITHUB_REPOSITORY" --state open --limit 1000 \
    --json number,headRefName,isCrossRepository \
    --jq '.[] | select(.isCrossRepository | not) | "\(.number) \(.headRefName)"') || {
    echo "::error::Could not list the open PRs."
    exit 2
  }
fi

# sibling_source <number> <tip>: succeeds, printing "#<m> (<branch>)", when the candidate is a sibling,
# not a parent (issue #116). A candidate is a parent when all its own unmerged commits are in this PR.
# When some aren't, it is a sibling if every unmerged commit it shares with this PR (each merge base
# not on develop) is also on another open PR's branch that isn't built on the candidate: the shared
# commits come from that PR, so setting the base to the candidate would be the wrong advice.
# A parent that moved on after this PR branched has no such other PR, so it keeps its base advice.
sibling_source() {
  local candidate=$1 tip=$2 number branch other base covered
  [ -n "$(git rev-list -n 1 "$tip" "^$develop_ref" "^$HEAD_SHA")" ] || return 1
  while read -r number branch; do
    [ -n "${number:-}" ] && [ -n "${branch:-}" ] || continue
    [ "$number" = "$candidate" ] || [ "$branch" = "${HEAD_BRANCH:-}" ] && continue
    other=$(git rev-parse --verify --quiet "${prefix}${branch}^{commit}") || continue
    git merge-base --is-ancestor "$tip" "$other" && continue # built on the candidate
    covered=1
    while read -r base; do
      [ -n "$base" ] || continue
      git merge-base --is-ancestor "$base" "$develop_ref" && continue
      git merge-base --is-ancestor "$base" "$other" || covered=0
    done < <(git merge-base --all "$tip" "$HEAD_SHA")
    [ "$covered" -eq 1 ] && { echo "#$number ($branch)"; return 0; }
  done <<<"${OPEN_PRS:-}"
  return 1
}

failed=0
while read -r number branch; do
  [ -n "${number:-}" ] && [ -n "${branch:-}" ] || continue
  [ "$branch" = "${HEAD_BRANCH:-}" ] && continue
  tip=$(git rev-parse --verify --quiet "${prefix}${branch}^{commit}") || continue
  [ "$number" -lt "${PR_NUMBER:?}" ] || continue # only a lower-numbered PR is a parent
  git merge-base --is-ancestor "$HEAD_SHA" "$tip" && continue # this PR is beneath that one
  shared_unmerged=0
  while read -r base; do
    [ -n "$base" ] || continue
    git merge-base --is-ancestor "$base" "$develop_ref" || shared_unmerged=1
  done < <(git merge-base --all "$tip" "$HEAD_SHA")
  [ "$shared_unmerged" -eq 1 ] || continue # only develop in common (or nothing)
  failed=1
  if sibling_of=$(sibling_source "$number" "$tip"); then
    echo "::error::This PR targets develop and shares unmerged commits with #$number ($branch), but #$number is not its parent: those commits also belong to ${sibling_of}."
    echo "Don't retarget this PR to $branch: follow the advice for the PR those commits come from."
    continue
  fi
  echo "::error::This PR targets develop but contains the commits of #$number ($branch), which aren't on develop."
  echo "Either merge #$number first, or set the base to $branch (retarget to develop after #$number merges). After you change the base, re-run this check: a base change does not start it."
done <<<"${OPEN_PRS:-}"

[ "$failed" -eq 0 ] && echo "No open PR's commits found beneath this PR."
exit "$failed"
