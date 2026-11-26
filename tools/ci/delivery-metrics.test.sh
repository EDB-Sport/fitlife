#!/usr/bin/env bash
# Tests for tools/ci/delivery-metrics.sh (issue #344): the four DORA delivery metrics from the release.yml and
# deploy.yml runs, on recorded API answers (tools/ci/delivery-metrics.fixtures/, the run history of v0.1.0 as
# recorded on 2026-11-26) and on made-up ones. A stub `gh` serves the fixtures, so nothing reaches GitHub.
# Usage: tools/ci/delivery-metrics.test.sh   (exit code 0 = every case passes)
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
script="$here/delivery-metrics.sh"
root="$here/../.."
workflow="$root/.github/workflows/delivery-metrics.yml"
failures=0
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

check() { # check <description> <command...>
  if "${@:2}"; then echo "PASS  $1"; else echo "FAIL  $1"; failures=$((failures + 1)); fi
}
has() { grep -qF -- "$1" <<<"$out"; } # has <text>: the last run's output contains the text

# A stub gh that answers `gh api [flags] <path>` from the files in $FIXTURES
mkdir -p "$tmp/bin"
cat >"$tmp/bin/gh" <<'STUB'
#!/usr/bin/env bash
for arg in "$@"; do case $arg in repos/*) path=${arg%%\?*} ;; esac; done
case $path in
  */actions/workflows/*/runs) cat "$FIXTURES/$(basename "$(dirname "$path")")-runs.json" ;;
  */actions/runs/*/jobs) id=$(basename "$(dirname "$path")"); cat "$FIXTURES/run-$id-jobs.json" 2>/dev/null || echo '{"jobs":[]}' ;;
  *) echo "stub gh: no fixture for $path" >&2; exit 1 ;;
esac
STUB
chmod +x "$tmp/bin/gh"

run() { # run <fixtures dir> [args...]: sets out and status
  out=$(env -u GH FIXTURES="$1" PATH="$tmp/bin:$PATH" "$script" "${@:2}" 2>&1)
  status=$?
}

# 1. v0.1.0 (the numbers in #344): 3 failed runs before the first deploy, about 2.5 h from the release merge to live
run "$here/delivery-metrics.fixtures" EDB-Sport/fitlife
check "v0.1.0: the script succeeds" [ "$status" -eq 0 ]
check "v0.1.0: the summary is a Markdown table" has "| Metric | Value |"
check "v0.1.0: deployment frequency is 1 deployment in 1 week" has "| Deployment frequency | 1 deployment in 1 week (1.0 per week) |"
check "v0.1.0: lead time is 2 h 31 min, release PR #299 merged 09:10Z to deploy 45605293186 done 11:41Z" \
  has "| Lead time (release PR merged → deploy succeeded) | 2 h 31 min (median of 1) |"
check "v0.1.0: change failure rate is 3 of 6 runs; pull_request and skipped runs don't count" \
  has "| Change failure rate (failed release and deploy runs ÷ all) | 3 of 6 runs (50%) |"
check "v0.1.0: time to restore is 2 h 8 min, failed deploy 09:33Z to the successful one at 11:41Z" \
  has "| Time to restore (failed deploy → next successful one) | 2 h 8 min (median of 1) |"

# 2. Made up: deploy.yml called by release.yml (#319) shows up as a "deploy / deploy" job in the release run
made=$tmp/made && mkdir -p "$made"
runs() { jq -n --argjson r "[$1]" '{workflow_runs: $r}'; }
release_run() { # release_run <id> <created> <updated> <conclusion>
  printf '{"id":%s,"event":"push","status":"completed","conclusion":"%s","created_at":"%s","updated_at":"%s"}' "$1" "$4" "$2" "$3"
}
deploy_job() { # deploy_job <conclusion> <completed>
  printf '{"jobs":[{"name":"plan","conclusion":"success","completed_at":"x"},{"name":"deploy / deploy","conclusion":"%s","completed_at":"%s"}]}' "$1" "$2"
}
runs "$(release_run 1 2026-12-01T10:00:00Z 2026-12-01T10:20:00Z failure),$(release_run 2 2026-12-01T12:00:00Z 2026-12-01T12:30:00Z success)" >"$made/release.yml-runs.json"
deploy_job failure 2026-12-01T10:20:00Z >"$made/run-1-jobs.json"
deploy_job success 2026-12-01T12:30:00Z >"$made/run-2-jobs.json"
# A redeploy by hand eight days later: a deployment, but it ships no new merge, so it has no lead time
runs '{"id":3,"event":"workflow_dispatch","status":"completed","conclusion":"success","created_at":"2026-12-09T09:00:00Z","updated_at":"2026-12-09T09:10:00Z"}' >"$made/deploy.yml-runs.json"
run "$made" EDB-Sport/fitlife
check "called deploy: the script succeeds" [ "$status" -eq 0 ]
check "called deploy: a release run's successful deploy job and a dispatched deploy are 2 deployments in 2 weeks" \
  has "| Deployment frequency | 2 deployments in 2 weeks (1.0 per week) |"
check "called deploy: lead time from the first merge since the last deployment, a redeploy without a merge has none" \
  has "| Lead time (release PR merged → deploy succeeded) | 2 h 30 min (median of 1) |"
check "called deploy: a release run is one run, with or without its deploy job" \
  has "| Change failure rate (failed release and deploy runs ÷ all) | 1 of 3 runs (33%) |"
check "called deploy: a failed deploy job in a release run starts the time to restore" \
  has "| Time to restore (failed deploy → next successful one) | 2 h 10 min (median of 1) |"

# 3. No runs at all: the table says so instead of failing
empty=$tmp/empty && mkdir -p "$empty"
runs '' >"$empty/release.yml-runs.json" && runs '' >"$empty/deploy.yml-runs.json"
run "$empty" EDB-Sport/fitlife
check "no runs: the script succeeds" [ "$status" -eq 0 ]
check "no runs: deployment frequency is 0" has "| Deployment frequency | 0 deployments |"
check "no runs: lead time has no value yet" has "| Lead time (release PR merged → deploy succeeded) | none yet |"

# 4. Without a repository it stops with the usage, not with a table of zeros
out=$(env -u GITHUB_REPOSITORY PATH="$tmp/bin:$PATH" "$script" 2>&1); status=$?
check "no repository: exit code 2" [ "$status" -eq 2 ]
check "no repository: it prints the usage" has "Usage:"

# 5. The workflow: started by hand or weekly, read-only, the table in the job summary
check "delivery-metrics.yml exists" test -f "$workflow"
check "the workflow can be started by hand" grep -qE '^  workflow_dispatch:' "$workflow"
check "the workflow runs weekly" grep -qE "^  schedule:" "$workflow"
check "the workflow's token can read the Actions runs" grep -qE '^  actions: read$' "$workflow"
check "the workflow's token writes nothing" bash -c 'test -f "$1" && ! grep -qE ": write" "$1"' _ "$workflow"
check "the workflow writes the script's table to the job summary" \
  grep -qF 'tools/ci/delivery-metrics.sh "$GITHUB_REPOSITORY" >> "$GITHUB_STEP_SUMMARY"' "$workflow"

echo
if [ "$failures" -eq 0 ]; then echo "All delivery-metrics.sh cases pass."; else echo "$failures case(s) failed."; fi
exit "$failures"
