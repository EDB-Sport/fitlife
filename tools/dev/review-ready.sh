#!/usr/bin/env bash
# One quick check of whether a PR is ready for review (issue #462). Not ready means no review:
# reviewers spend a full review turn on what this sees in seconds.
# Usage: tools/dev/review-ready.sh <pr>
#   exit 0  ready; the info lines (behind develop) are not reasons
#   exit 1  not ready: one "not ready: <reason>" line per reason
#   exit 2  could not check (gh failed)
# GH names the GitHub CLI (default gh; on WSL set GH=gh.exe).
# Reasons: a draft PR; a required check (the base's ruleset: main, else develop) that isn't
# SUCCESS or SKIPPED on the head; a conflict with the base; a base branch that is neither develop
# nor main and has no open PR (a broken stack); a workflow under .github/workflows/ that changed on
# develop after the merge base (CI on the head is stale).
set -uo pipefail

repo=EDB-Sport/fitlife
gh_cmd=${GH:-gh}
pr=${1:?usage: tools/dev/review-ready.sh <pr>}

pr_json=$("$gh_cmd" pr view "$pr" -R "$repo" \
  --json isDraft,mergeable,mergeStateStatus,baseRefName,headRefOid,statusCheckRollup,body) || {
  echo "error: could not read PR #$pr" >&2
  exit 2
}
base=$(jq -r .baseRefName <<<"$pr_json")
head=$(jq -r .headRefOid <<<"$pr_json")

ruleset=develop
[ "$base" = main ] && ruleset=main
required=$("$gh_cmd" api "repos/$repo/rules/branches/$ruleset" \
  --jq '[.[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context] | unique | .[]') || {
  echo "error: could not read the $ruleset ruleset" >&2
  exit 2
}
if [ -z "$required" ]; then
  echo "error: the $ruleset ruleset lists no required checks" >&2
  exit 2
fi

reasons=()
info=()

[ "$(jq -r .isDraft <<<"$pr_json")" = true ] && reasons+=("draft PR: mark it ready for review")

# The last run of a check name wins (a re-run replaces the earlier run). A status context has
# .context and .state; a check run has .name and .status/.conclusion.
while IFS= read -r check; do
  entry=$(jq -r --arg n "$check" '
    [(.statusCheckRollup // [])[]
      | {name: (.name // .context),
         state: (if .__typename == "StatusContext" then .state
                 elif .status == "COMPLETED" then .conclusion
                 else .status end),
         link: (.detailsUrl // .targetUrl // "")}]
    | map(select(.name == $n)) | last // empty
    | "\(.state)\t\(.link)"' <<<"$pr_json")
  if [ -z "$entry" ]; then
    reasons+=("required check $check has no run on the head")
    continue
  fi
  state=${entry%%$'\t'*}
  link=${entry#*$'\t'}
  case "$state" in
    SUCCESS|SKIPPED) ;;
    *) reasons+=("required check $check is $state on the head${link:+: $link}") ;;
  esac
done <<<"$required"

[ "$(jq -r .mergeable <<<"$pr_json")" = CONFLICTING ] && reasons+=("conflicts with $base: merge or rebase it")

if [ "$base" != develop ] && [ "$base" != main ]; then
  open_heads=$("$gh_cmd" pr list -R "$repo" --state open --limit 1000 \
    --json headRefName --jq '.[].headRefName') || {
    echo "error: could not list the open PRs" >&2
    exit 2
  }
  grep -qxF -- "$base" <<<"$open_heads" \
    || reasons+=("base $base has no open PR: a broken stack; retarget the PR to develop or reopen its parent")
fi

# Behind develop is information only, unless a workflow changed on develop since the merge base:
# then CI on the head is stale. The compare is head...develop, so its files are the develop side
# and ahead_by counts the develop commits this PR doesn't have.
behind_json=$("$gh_cmd" api "repos/$repo/compare/$head...develop") || {
  echo "error: could not compare the head with develop" >&2
  exit 2
}
behind=$(jq -r .ahead_by <<<"$behind_json")
if [ "$behind" -gt 0 ]; then
  info+=("info: $behind commit(s) behind develop (not a reason: the ruleset isn't strict)")
  workflows=$(jq -r '[(.files // [])[] | .filename | select(startswith(".github/workflows/"))] | join(", ")' <<<"$behind_json")
  [ -n "$workflows" ] && reasons+=("CI on the head is stale: develop changed $workflows since the merge base; update the branch from develop")
fi

for line in ${info[@]+"${info[@]}"}; do echo "$line"; done
if [ ${#reasons[@]} -eq 0 ]; then
  echo "PR #$pr is ready for review."
  exit 0
fi
for reason in "${reasons[@]}"; do echo "not ready: $reason"; done
exit 1
