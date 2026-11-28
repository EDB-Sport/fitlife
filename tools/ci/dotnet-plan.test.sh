#!/usr/bin/env bash
# Tests of tools/ci/dotnet-plan.sh and its wiring in .github/workflows/ci.yml (issue #568): a PR whose changed files
# are all Markdown that the build and the tests don't read skips the .NET jobs; anything else runs them.
# The plan fails open: an empty file list, a failed plan or a skipped plan (push runs) runs every job, because a job
# skipped through a failed need shows green to a required check.
# Usage: tools/ci/dotnet-plan.test.sh   (exit 0 = every check passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
plan="$root/tools/ci/dotnet-plan.sh"
ci="$root/.github/workflows/ci.yml"
failures=0

# expect_plan <description> <want: run|skip> <changed path>...
expect_plan() {
  local description=$1 want=$2 got
  shift 2
  got=$(printf '%s\n' "$@" | "$plan" 2>&1)
  if [ "$got" = "$want" ]; then
    echo "PASS  $description"
  else
    echo "FAIL  $description"
    echo "  wanted: $want; got: $got"
    failures=$((failures + 1))
  fi
}

expect_plan "Markdown docs only skip the .NET jobs" skip docs/x.md README.md
expect_plan "a Markdown file in a service folder skips too" skip src/BookingService/AGENTS.md
expect_plan "GLOSSARY.md runs them: the build passes it to the TEAM analyzers" run GLOSSARY.md
expect_plan "AnalyzerReleases.Unshipped.md runs them: the analyzer project reads it" run \
  tools/Analyzers/Team.Analyzers/AnalyzerReleases.Unshipped.md
expect_plan "AnalyzerReleases.Shipped.md runs them" run tools/Analyzers/Team.Analyzers/AnalyzerReleases.Shipped.md
expect_plan "docs/domain/ACCESS.md runs them: a test reads it (#489)" run docs/domain/ACCESS.md
for path in src/A/B.cs src/A/A.csproj Directory.Build.props Directory.Build.targets src/A/packages.lock.json \
  .github/workflows/ci.yml tools/ci/x.sh .editorconfig global.json; do
  expect_plan "$path runs them" run "$path"
done
expect_plan "Markdown plus one C# file runs them" run docs/x.md src/A/B.cs
expect_plan "an empty file list runs them (fail open)" run ""
got=$("$plan" </dev/null)
if [ "$got" = run ]; then echo "PASS  no input at all runs them (fail open)"; else
  echo "FAIL  no input at all runs them (fail open)"
  echo "  got: $got"
  failures=$((failures + 1))
fi

# The run list can't go stale: every Markdown file an AdditionalFiles entry or a string in a file under tests/ names is run
while IFS= read -r name; do
  [ -n "$name" ] || continue
  expect_plan "$name, read by the build or a test, runs them" run "some/folder/$name"
done < <(
  {
    git -C "$root" ls-files '*.props' '*.targets' '*.csproj' | sed "s|^|$root/|" | xargs grep -ohE '<AdditionalFiles Include="[^"]*\.md"' |
      sed -E 's/.*[/")]([^/")]+\.md)"$/\1/'
    git -C "$root" ls-files 'tests/*.cs' | sed "s|^|$root/|" | xargs grep -ohE '"[^"]*\.md"' 2>/dev/null |
      sed -E 's/.*[/"]([^/"]+\.md)"$/\1/'
  } | sort -u
)

# job_block <job name>: the lines of one ci.yml job, from "  <name>:" to the next job
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

plan_job=$(job_block plan)
expect "ci.yml has a plan job" test -n "$plan_job"
expect "plan runs on pull requests only, so push runs on develop and main run every job" \
  grep -qF "if: github.event_name == 'pull_request'" <<<"$plan_job"
expect "plan runs tools/ci/dotnet-plan.sh on the PR's diff" \
  grep -qE 'git diff --name-only "\$BASE_SHA\.\.\.\$HEAD_SHA" \| tools/ci/dotnet-plan\.sh' <<<"$plan_job"
expect "plan pipes with pipefail and prints run when the pipe fails (fail open)" \
  bash -c 'grep -qF "set -o pipefail" <<<"$1" && grep -qF "|| dotnet=run" <<<"$1"' _ "$plan_job"
expect "plan has no continue-on-error" bash -c '! grep -q "continue-on-error" <<<"$1"' _ "$plan_job"

for job in restore build format test contract build-rules; do
  block=$(job_block "$job")
  expect "$job needs plan" grep -qE '^    needs: (plan|\[plan, build\])( #.*)?$' <<<"$block"
  expect "$job runs unless the plan says skip, also when the plan failed or was skipped" \
    bash -c 'grep -E "^    if: " <<<"$1" | grep -F "!cancelled() && " | grep -qF "needs.plan.outputs.dotnet != '"'skip'"'"' _ "$block"
done
for job in format test contract; do
  expect "$job still runs only after a green build (issue #134)" \
    bash -c 'grep -qE "^    needs: \[plan, build\]" <<<"$1" && grep -qF "needs.build.result == '"'success'"'" <<<"$1"' _ "$(job_block "$job")"
done
for job in ci-scripts stack-check diff-scan; do
  expect "$job doesn't wait for the plan" bash -c '! grep -qE "^    needs:.*plan" <<<"$1"' _ "$(job_block "$job")"
done

exit $((failures > 0))
