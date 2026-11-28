#!/usr/bin/env bash
# Tests for tools/ci/summarize.sh (issue #132): a failing CI step writes a short summary, a passing one doesn't.
# Usage: tools/ci/summarize.test.sh   (exit code 0 = every case passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
script="$here/summarize.sh"
root="$here/../.."
failures=0
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

check() { # check <description> <command...>
  if "${@:2}"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1"
    test_report_got "${@:2}"
    test_report_tail out "${out:-}"
    test_report_tail summary "${summary:-}"
    failures=$((failures + 1))
  fi
}

# run <command...>: summarize.sh around the command, with a fresh summary file. Sets out, status and summary.
run() {
  summary_file="$tmp/summary-$RANDOM.md"
  out=$(GITHUB_STEP_SUMMARY="$summary_file" "$script" build "Fix the first compiler error; the others often follow from it." -- "$@" 2>&1)
  status=$?
  summary=$(cat "$summary_file" 2>/dev/null || true)
}

# 1. A failing step: the summary names the check, the first real error and the hint, and the exit code stays
run bash -c 'echo "  Determining projects to restore..."
echo "/repo/src/Program.cs(12,5): error CS0103: The name '\''Foo'\'' does not exist [/repo/src/App.csproj]"
echo "/repo/src/Other.cs(3,1): error CS0246: The type '\''Bar'\'' could not be found [/repo/src/App.csproj]"
exit 3'
check "a failing step keeps its exit code" [ "$status" -eq 3 ]
check "a failing step still prints its output" grep -qF "error CS0246" <<<"$out"
check "the summary names the failing check" grep -qF "build" <<<"$summary"
check "the summary has the first real error line" grep -qF "error CS0103: The name 'Foo' does not exist" <<<"$summary"
check "the summary has only the first error" bash -c '! grep -qF "CS0246" <<<"$1"' _ "$summary"
check "the summary has the hint" grep -qF "Fix the first compiler error" <<<"$summary"

check "a failing step prints the error as an annotation, which gh run view lists" \
  grep -qF "::error title=build failed::/repo/src/Program.cs(12,5): error CS0103: The name 'Foo' does not exist" <<<"$out"

# 2. Failing tests and Newman assertions are found too
run bash -c 'echo "Starting test execution, please wait..."; echo "  Failed Reserve_FullClass_IsRefused [12 ms]"; echo "  Error Message:"; exit 1'
check "a failed test is the error line" grep -qF "Failed Reserve_FullClass_IsRefused" <<<"$summary"
run bash -c 'echo "→ Create account"; echo "  1. AssertionError  Status is 201"; echo "     expected 500 to equal 201"; exit 1'
check "a Newman assertion is the error line" grep -qF "AssertionError  Status is 201" <<<"$summary"

# 2b. dotnet format's error lines have no digits in the code (issue #132 review)
run bash -c 'echo "/repo/src/Program.cs(4,1): error WHITESPACE: Fix whitespace formatting. [/repo/src/App.csproj]"; exit 2'
check "a dotnet format error is the error line" grep -qF "error WHITESPACE: Fix whitespace formatting." <<<"$summary"

# 2c. % and newlines in the error are escaped in the annotation, so it stays one line
run bash -c 'echo "x.cs(1,1): error CS1000: 100% wrong"; exit 1'
check "a % in the error is escaped in the annotation" grep -qF "::error title=build failed::x.cs(1,1): error CS1000: 100%25 wrong" <<<"$out"

# 3. A failure without a recognisable error line still gets a summary, with the last output line
run bash -c 'echo "something went wrong"; exit 1'
check "without an error line the summary has the last output line" grep -qF "something went wrong" <<<"$summary"

# 4. A passing step adds nothing and passes
run bash -c 'echo "Build succeeded."'
check "a passing step passes" [ "$status" -eq 0 ]
check "a passing step writes no summary" [ -z "$summary" ]

# 5. Outside GitHub Actions (no GITHUB_STEP_SUMMARY) it only runs the command
out=$(env -u GITHUB_STEP_SUMMARY "$script" build "hint" -- bash -c 'echo "x: error E1: y"; exit 2' 2>&1); status=$?
check "without GITHUB_STEP_SUMMARY it keeps the exit code" [ "$status" -eq 2 ]

# 6. The workflows use it for the jobs with the largest logs
for job in build format test; do
  check "ci.yml's $job job runs through summarize.sh" \
    grep -qE "run: tools/ci/summarize\.sh $job " "$root/.github/workflows/ci.yml"
done
check "api-tests runs through summarize.sh" grep -qE "run: tools/ci/summarize\.sh api-tests " "$root/.github/workflows/api-tests.yml"
check "AGENTS.md says to read the run summary first" grep -qF "gh run view --log-failed" "$root/AGENTS.md"

exit "$failures"
