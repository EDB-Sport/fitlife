#!/usr/bin/env bash
# Delivery metrics (DORA) from the Actions history of release.yml and deploy.yml (issue #344), for the report.
# Prints a Markdown table; .github/workflows/delivery-metrics.yml writes it to the job summary.
#
# Usage: tools/ci/delivery-metrics.sh [<owner>/<repo>]   (default: $GITHUB_REPOSITORY)
#        On WSL with the Windows CLI: GH=gh.exe tools/ci/delivery-metrics.sh EDB-Sport/fitlife
#
# What counts:
# - A run is a release.yml run on a push to main (a merged release or hotfix PR) or any deploy.yml run, that
#   ended in success, failure or timed_out. release.yml's pull_request runs are the release gate, not a
#   delivery, and skipped or cancelled runs did nothing; neither counts.
# - A deployment is a successful deploy.yml run, or a release run whose called "deploy / ..." job succeeded
#   (release.yml calls deploy.yml since #319). A failed deploy is the same, failed.
# - Deployment frequency: deployments per week, from the first run to the last.
# - Lead time: from the first release merge since the previous deployment to the deployment's end. A
#   deployment without a new merge (a redeploy or rollback by hand) ships nothing new and has none. Median.
# - Change failure rate: failed runs ÷ all runs.
# - Time to restore: from the first failed deploy since the previous deployment to the next deployment's end.
#   Median.
set -euo pipefail

repo=${1:-${GITHUB_REPOSITORY:-}}
if [ -z "$repo" ]; then
  echo "Usage: $0 <owner>/<repo>   (or set GITHUB_REPOSITORY)" >&2
  exit 2
fi
command -v jq >/dev/null || { echo "delivery-metrics.sh needs jq" >&2; exit 2; }
gh=${GH:-gh}

# runs <workflow file>: its completed runs, one JSON object per line. tr: gh.exe on WSL ends lines with CRLF.
runs() {
  "$gh" api --paginate "repos/$repo/actions/workflows/$1/runs?per_page=100" | tr -d '\r' |
    jq -c '.workflow_runs[] | select(.status == "completed")'
}

release_runs=$(runs release.yml | jq -c 'select(.event == "push")' | jq -sc .)
deploy_runs=$(runs deploy.yml | jq -sc .)

# The called deploy job of each release run, by run id ({} when it has none)
deploy_jobs='{}'
for id in $(jq -r '.[].id' <<<"$release_runs"); do
  job=$("$gh" api --paginate "repos/$repo/actions/runs/$id/jobs?per_page=100" | tr -d '\r' |
    jq -sc '[.[].jobs[] | select(.name | test("^deploy( / |$)"))] | first // {}')
  deploy_jobs=$(jq -c --arg id "$id" --argjson job "$job" '.[$id] = $job' <<<"$deploy_jobs")
done

jq -nr --argjson release "$release_runs" --argjson deploy "$deploy_runs" --argjson jobs "$deploy_jobs" '
  def t: fromdateiso8601;
  def ended: . == "success" or . == "failure" or . == "timed_out";
  def failed: . == "failure" or . == "timed_out";
  def duration: (. / 60 | floor) as $m
    | if $m >= 1440 then "\($m / 1440 | floor) d \($m % 1440 / 60 | floor) h"
      elif $m >= 60 then "\($m / 60 | floor) h \($m % 60) min"
      else "\($m) min" end;
  def median: sort | length as $n
    | if $n % 2 == 1 then .[($n - 1) / 2] else (.[$n / 2 - 1] + .[$n / 2]) / 2 end;
  def plural($n; $word): "\($n) \($word)\(if $n == 1 then "" else "s" end)";
  def medians: if length == 0 then "none yet" else "\(median | duration) (median of \(length))" end;

  ( [ $release[] | {merge: true, start: (.created_at | t), done: (.updated_at | t), conclusion,
        deploy: ($jobs[.id | tostring].conclusion // null), deployed: ($jobs[.id | tostring].completed_at // null)} ]
  + [ $deploy[] | {merge: false, start: (.created_at | t), done: (.updated_at | t), conclusion, deploy: .conclusion, deployed: .updated_at} ]
  | map(select(.conclusion | ended))
  | map(.end = (.deployed // null | if . then t else null end)) ) as $runs
  | ([ $runs[] | select(.deploy == "success") | .end ] | sort) as $deployments
  | [ $runs[] | select(.merge) | .start ] as $merges
  | [ $runs[] | select(.deploy | failed) | .end ] as $failed_deploys
  # For each deployment: the times since the previous one
  | [ range(0; $deployments | length) as $i
      | ($deployments[$i]) as $at | (if $i == 0 then -1 else $deployments[$i - 1] end) as $prev
      | { at: $at,
          merges: [ $merges[] | select(. > $prev and . <= $at) ],
          failures: [ $failed_deploys[] | select(. > $prev and . < $at) ] } ] as $windows
  | [ $windows[] | select(.merges | length > 0) | .at - (.merges | min) ] as $lead_times
  | [ $windows[] | select(.failures | length > 0) | .at - (.failures | min) ] as $restores
  | ($runs | length) as $total
  | ([ $runs[] | select(.conclusion | failed) ] | length) as $failures
  | ($deployments | length) as $n
  | (if $total == 0 then 1
     else ((([ $runs[].done ] | max) - ([ $runs[].start ] | min)) / 604800 | ceil
           | if . < 1 then 1 else . end) end) as $weeks
  | "| Metric | Value |",
    "|---|---|",
    "| Deployment frequency | \(if $n == 0 then "0 deployments"
        else "\(plural($n; "deployment")) in \(plural($weeks; "week")) (\($n * 10 / $weeks | round / 10
          | tostring | if test("[.]") then . else . + ".0" end) per week)" end) |",
    "| Lead time (release PR merged → deploy succeeded) | \($lead_times | medians) |",
    "| Change failure rate (failed release and deploy runs ÷ all) | \(if $total == 0 then "no runs yet"
        else "\($failures) of \(plural($total; "run")) (\($failures * 100 / $total | round)%)" end) |",
    "| Time to restore (failed deploy → next successful one) | \($restores | medians) |"
'
