#!/usr/bin/env bash
# Checks that .github/workflows/ci.yml runs the CI scripts that aren't *.test.sh (issue #54),
# and that it never cancels a push run on develop or main (issue #131).
# Usage: tools/ci/ci-workflow.test.sh   (exit code 0 = every check passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
ci="$root/.github/workflows/ci.yml"
failures=0

# job_block <job name>: the lines of one job, from "  <name>:" to the next job
job_block() {
  awk -v job="  $1:" '
    $0 == job { inside = 1; print; next }
    inside && /^  [A-Za-z0-9_-]+:/ { exit }
    inside { print }
  ' "$ci"
}

# expect <description> <command...>
expect() {
  local description=$1
  shift
  if "$@"; then
    echo "PASS  $description"
  else
    echo "FAIL  $description"
    failures=$((failures + 1))
  fi
}

# One compile error fails build only: format and test wait for it and are skipped (issue #134).
# The job names stay the same, so required checks keep working.
for job in build format test; do
  expect "ci.yml has a $job job" test -n "$(job_block "$job")"
done
for job in format test; do
  expect "$job runs only after build" grep -qE '^    needs: (\[?build\]?|\[plan, build\])$' <<<"$(job_block "$job")"
done

# contract compiles the services too: one compile error fails build only (issue #322)
expect "contract runs only after build" grep -qE '^    needs: (\[?build\]?|\[plan, build\])( #.*)?$' <<<"$(job_block contract)"

build_rules=$(job_block build-rules)

expect "ci.yml runs on pull requests" grep -qE '^  pull_request:' "$ci"
# A newer push cancels a PR's run, but never a push run on develop or main (issue #131):
# close merges would cancel each other's runs and leave a merge without a verdict.
expect "ci.yml cancels in-progress runs for pull requests only" \
  grep -qF "cancel-in-progress: \${{ github.event_name == 'pull_request' }}" "$ci"
expect "ci.yml has a build-rules job" test -n "$build_rules"
expect "build-rules runs tools/ci/check-build-rules.sh" \
  grep -qE 'run: (tools/ci/summarize\.sh .* -- )?tools/ci/check-build-rules\.sh$' <<<"$build_rules"
expect "build-rules sets up the SDK pinned in global.json" \
  grep -qE 'global-json-file: global\.json' <<<"$build_rules"
expect "build-rules has no continue-on-error" \
  bash -c '! grep -q "continue-on-error" <<<"$1"' _ "$build_rules"

stack_check=$(job_block stack-check)

expect "ci.yml has a stack-check job" test -n "$stack_check"
expect "stack-check runs tools/ci/stack-check.sh" \
  grep -qE 'run: (tools/ci/summarize\.sh .* -- )?tools/ci/stack-check\.sh$' <<<"$stack_check"
expect "stack-check runs on pull requests only" \
  grep -qF "if: github.event_name == 'pull_request'" <<<"$stack_check"
expect "stack-check checks out the full history" \
  grep -qE 'fetch-depth: 0' <<<"$stack_check"
expect "stack-check can read pull requests" \
  grep -qE 'pull-requests: read' <<<"$stack_check"
expect "stack-check cannot write anything" \
  bash -c '! grep -qE "(contents|issues|pull-requests): write" <<<"$1"' _ "$stack_check"

# Every job stops after a bounded time, so a hang can't hold a runner for GitHub's default of 360 minutes (issue #276).
# jobs_without_timeout <workflow file>: the names of the jobs that set no timeout-minutes
jobs_without_timeout() {
  awk '
    /^jobs:/ { in_jobs = 1; next }
    in_jobs && /^[^ #]/ { in_jobs = 0 }
    in_jobs && /^  [A-Za-z0-9_-]+:/ { if (job != "" && !has) print job; job = $1; sub(/:$/, "", job); has = 0; next }
    in_jobs && /^    timeout-minutes: [0-9]+$/ { has = 1 }
    # A job that calls a reusable workflow (uses:) cannot set timeout-minutes; the called job sets it (#319)
    in_jobs && /^    uses: \.\/\.github\/workflows\/[A-Za-z0-9_-]+\.ya?ml$/ { has = 1 }
    END { if (job != "" && !has) print job }
  ' "$1"
}
for workflow in "$root"/.github/workflows/*.yml; do
  name=$(basename "$workflow")
  missing=$(jobs_without_timeout "$workflow" | tr '\n' ' ')
  expect "every job in $name sets timeout-minutes${missing:+ (missing: $missing)}" test -z "$missing"
done

# Service PRs with Testcontainers integration tests run mutation for 8-20 min (max 20.2 min), so a 10-minute limit cancels them (issue #335).
mutation_timeout=$(sed -n 's/^    timeout-minutes: \([0-9][0-9]*\)$/\1/p' <<<"$(job_block mutation)")
expect "mutation sets timeout-minutes of at least 30 (issue #335)" test "${mutation_timeout:-0}" -ge 30

# Mutation testing is switched on by FITLIFE_MUTATION=1 alone, so CI and local runs of the script tests agree (issue #461).
# The mutation job runs only with its original condition; ci-scripts sets FITLIFE_MUTATION=1 only then.
mutation_enabled=0
grep -qF "if: github.event_name == 'pull_request'" <<<"$(job_block mutation)" && mutation_enabled=1
ci_scripts_switch=0
grep -qE '^ +FITLIFE_MUTATION: "?1"?$' <<<"$(job_block ci-scripts)" && ci_scripts_switch=1
expect "ci-scripts sets FITLIFE_MUTATION=1 exactly when the mutation job runs (job: $mutation_enabled, ci-scripts: $ci_scripts_switch)" \
  test "$mutation_enabled" = "$ci_scripts_switch"
expect "ci-scripts runs every tools/ci/*.test.sh, with no special case for mutation.test.sh" \
  bash -c '! grep -qF "mutation.test.sh" <<<"$1"' _ "$(job_block ci-scripts | grep -v '^ *#\|- uses:')"
if [ "$mutation_enabled" = 0 ]; then
  skipped=$(env -u FITLIFE_MUTATION timeout 30 "$root/tools/ci/mutation.test.sh" 2>&1)
  skipped_status=$?
  expect "a local run of mutation.test.sh exits 0 while mutation is disabled (got $skipped_status)" test "$skipped_status" -eq 0
  expect "a local run of mutation.test.sh says it is skipped while mutation is disabled" \
    grep -qF "skipped: mutation testing is disabled" <<<"$skipped"
fi

# A hanging test is killed after 5 minutes and named, without a dump file (issue #276).
hang_flags='--blame-hang-timeout 5m --blame-hang-dump-type none'
expect "ci.yml's test step passes $hang_flags" \
  grep -qF "dotnet test -c Release --no-build $hang_flags" <<<"$(job_block test)"
expect "tdd-check.yml's TEST_CMD passes $hang_flags in both runs" \
  test "$(grep -cF "TEST_CMD: dotnet test -c Release $hang_flags" "$root/.github/workflows/tdd-check.yml")" -eq 2

# A red CI run names the failing script or check and its first error in gh run view (issue #376).
# The ci-scripts loop annotates each failing script by name.
expect "ci-scripts annotates each failing script as ::error title=ci-scripts::<script> failed" \
  grep -qF 'echo "::error title=ci-scripts::$test failed' <<<"$(job_block ci-scripts)"
# The other checks run through tools/ci/summarize.sh with a hint, so a failure gets an annotation and a summary.
# runs_through_summarize <job> <check> <script>: the job runs <script> as summarize.sh <check> "<hint>" -- <script>
runs_through_summarize() {
  grep -qE "run: tools/ci/summarize\.sh $2 \"[^\"]+\" -- tools/ci/$3\$" <<<"$(job_block "$1")"
}
for pair in stack-check:stack-check.sh mutation:mutation.sh build-rules:check-build-rules.sh \
  contract:check-contracts.sh contract:oasdiff-breaking.sh diff-scan:diff-scan.sh; do
  job=${pair%%:*} script=${pair#*:}
  expect "$job runs tools/ci/$script through summarize.sh with a hint" runs_through_summarize "$job" "$job" "$script"
done

# The ci-scripts job prints only what is wrong: FAIL lines and one summary per script; VERBOSE=1 adds the PASS lines (issue #375).
# Runs the job's own run block on a copy of tools/ci/test-report.sh and one *.test.sh with a passing and a failing case.
ci_scripts_run=$(job_block ci-scripts | awk '
  /^        run: \|$/ { inside = 1; next }
  inside && /^      - / { exit }
  inside { sub(/^          /, ""); print }
')
job_repo=$(mktemp -d)
trap 'rm -rf "$job_repo"' EXIT
mkdir -p "$job_repo/tools/ci"
cp "$root/tools/ci/test-report.sh" "$job_repo/tools/ci/"
printf '%s\n' '#!/usr/bin/env bash' '. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"' \
  'echo "PASS  one"' 'echo "FAIL  two: expected 1, got 2"' 'exit 1' >"$job_repo/tools/ci/sample.test.sh"
chmod +x "$job_repo/tools/ci/sample.test.sh"
# A tools/dev test of any name, to see that the job picks it up without being edited (issue #488).
mkdir -p "$job_repo/tools/dev"
printf '%s
' '#!/usr/bin/env bash' 'echo "FAIL  dev tool case"' 'exit 1' >"$job_repo/tools/dev/any-name.test.sh"
chmod +x "$job_repo/tools/dev/any-name.test.sh"
quiet=$(cd "$job_repo" && env -u TEST_REPORT_RAW -u VERBOSE bash -c "$ci_scripts_run" 2>&1)
verbose=$(cd "$job_repo" && env -u TEST_REPORT_RAW VERBOSE=1 bash -c "$ci_scripts_run" 2>&1)
expect "ci-scripts hides the PASS lines by default" bash -c '! grep -q "^PASS" <<<"$1"' _ "$quiet"
expect "ci-scripts prints the FAIL lines by default" grep -qxF "FAIL  two: expected 1, got 2" <<<"$quiet"
expect "ci-scripts prints one summary line per script" grep -qxF "2 cases, 1 failed" <<<"$quiet"
expect "ci-scripts with VERBOSE=1 prints the PASS lines" grep -qxF "PASS  one" <<<"$verbose"
expect "ci-scripts with VERBOSE=1 prints the FAIL lines" grep -qxF "FAIL  two: expected 1, got 2" <<<"$verbose"
expect "ci-scripts runs every tools/dev/*.test.sh" grep -qxF "FAIL  dev tool case" <<<"$quiet"
expect "ci-scripts annotates a failing tools/dev test by name"   grep -qF "::error title=ci-scripts::tools/dev/any-name.test.sh failed" <<<"$quiet"

exit "$failures"
