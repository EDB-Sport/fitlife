#!/usr/bin/env bash
# Fails a pull request that targets develop but is built on another open PR's branch (issue #110).
# A branch counts as the parent when its tip is in this PR's history and not yet on develop.
# Inputs (environment):
#   BASE_REF     the PR's base branch; only a PR whose base is develop is checked
#   HEAD_SHA     the PR's head commit
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

failed=0
while read -r number branch; do
  [ -n "${number:-}" ] && [ -n "${branch:-}" ] || continue
  [ "$branch" = "${HEAD_BRANCH:-}" ] && continue
  tip=$(git rev-parse --verify --quiet "${prefix}${branch}^{commit}") || continue
  [ "$tip" = "$HEAD_SHA" ] && continue
  git merge-base --is-ancestor "$tip" "$develop_ref" && continue # parent already merged
  git merge-base --is-ancestor "$tip" "$HEAD_SHA" || continue    # not in this PR's history
  echo "::error::This PR targets develop but contains the commits of #$number ($branch), which aren't on develop."
  echo "Either merge #$number first, or set the base to $branch (retarget to develop after #$number merges)."
  failed=1
done <<<"${OPEN_PRS:-}"

[ "$failed" -eq 0 ] && echo "No open PR's commits found beneath this PR."
exit "$failed"
