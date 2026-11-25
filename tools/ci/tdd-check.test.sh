#!/usr/bin/env bash
# Tests for tools/ci/tdd-check.sh and its workflow (issue #43).
# Each case builds a small fixture repo (a base commit and a PR commit) and runs the check on it.
# The fixture's test suite is a stand-in for `dotnet test`: every `// needs: <word>` line in
# tests/ must appear somewhere under src/, so a test "fails on base" when its word isn't there yet.
# Usage: tools/ci/tdd-check.test.sh   (exit code 0 = all cases pass)
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
check="$here/tdd-check.sh"
workflow="$here/../../.github/workflows/tdd-check.yml"
failures=0
tmp_root=$(mktemp -d)
trap 'rm -rf "$tmp_root"' EXIT

# fixture: a new repo with one passing test on the base commit. Prints its path.
fixture() {
  local repo
  repo=$(mktemp -d "$tmp_root/repo.XXXXXX")
  git -C "$repo" init --quiet --initial-branch=develop
  git -C "$repo" config user.email test@example.invalid
  git -C "$repo" config user.name tdd-check-test
  mkdir -p "$repo/src" "$repo/tests"
  cat > "$repo/run-tests.sh" <<'EOF'
#!/usr/bin/env bash
status=0
while read -r word; do
  grep -rqF "$word" src/ 2>/dev/null || { echo "red: $word"; status=1; }
done < <(grep -rhoE '// needs: [A-Za-z]+' tests/ | sed 's|// needs: ||')
exit "$status"
EOF
  cat > "$repo/src/Calculator.cs" <<'EOF'
public static class Calculator { public static int Add(int a, int b) => a + b; }
EOF
  cat > "$repo/tests/CalculatorTests.cs" <<'EOF'
public class CalculatorTests
{
    [Test] // needs: Add
    public void Add_TwoNumbers_ReturnsSum() { }

    [TestCase(1, 2)]
    [TestCase(2, 3)]
    public void Add_Cases_ReturnSum(int a, int b) { }
}
EOF
  commit "$repo" "base"
  echo "$repo"
}

commit() { git -C "$1" add -A && git -C "$1" commit --quiet -m "$2"; }

# expect <description> <pass|fail> <repo> [text the output must contain] [labels]
expect() {
  local out status
  out=$(cd "$3" && BASE_SHA=$(git rev-parse HEAD^) TEST_CMD="bash run-tests.sh" PR_LABELS="${5:-}" "$check" 2>&1)
  status=$?
  if [ "$2" = pass ] && [ "$status" -eq 0 ]; then
    echo "PASS  $1"
  elif [ "$2" = fail ] && [ "$status" -ne 0 ] && grep -qF -- "${4:-}" <<<"$out"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1: expected $2${4:+ with '$4'}, got exit $status"
    echo "$out" | sed 's/^/      /'
    failures=$((failures + 1))
  fi
}

# A feature PR: a new test that is red on base, and the code that makes it green.
feature_pr() {
  local repo; repo=$(fixture)
  cat >> "$repo/tests/CalculatorTests.cs" <<'EOF'
public class SubtractTests
{
    [Test] // needs: Subtract
    public void Subtract_TwoNumbers_ReturnsDifference() { }
}
EOF
  echo 'public static class Subtractor { public static int Subtract(int a, int b) => a - b; }' > "$repo/src/Subtractor.cs"
  commit "$repo" "feature"
  echo "$repo"
}

# A refactor PR: production code moved and reshaped, behaviour and tests unchanged.
refactor_pr() {
  local repo; repo=$(fixture)
  mkdir -p "$repo/src/Arithmetic"
  git -C "$repo" mv src/Calculator.cs src/Arithmetic/Calculator.cs
  cat > "$repo/src/Arithmetic/Calculator.cs" <<'EOF'
public static class Calculator
{
    public static int Add(int a, int b) => a + b;
}
EOF
  commit "$repo" "refactor"
  echo "$repo"
}

# 1. Feature PRs
expect "feature PR passes" pass "$(feature_pr)"
expect "feature PR with a refactor label is still checked as a refactor" fail "$(feature_pr)" "changes files under tests/" refactor

# 2. Refactor PRs (METHODOLOGY section 8)
expect "refactor PR with the refactor label passes" pass "$(refactor_pr)" "" refactor
expect "refactor label among other labels passes" pass "$(refactor_pr)" "" "setup,refactor"
expect "refactor PR without the label fails" fail "$(refactor_pr)" "no test under tests/ was added or changed"

repo=$(refactor_pr)
echo "// a note" >> "$repo/tests/CalculatorTests.cs"
commit "$repo" "touch a test"
expect "refactor PR that changes a test fails" fail "$repo" "changes files under tests/" refactor

# A refactor that breaks behaviour: tests unchanged, but the suite goes red.
repo=$(fixture)
echo 'public static class Calculator { public static int Sum(int a, int b) => a + b; }' > "$repo/src/Calculator.cs"
commit "$repo" "refactor that breaks the suite"
expect "refactor PR with a red suite fails" fail "$repo" "the tests fail" refactor

# edit <file> <sed script>: in-place edit that works with GNU and BSD sed (macOS, #87).
# GNU and BSD disagree on "sed -i" and on "\n" in a replacement; a backslash-newline works in both.
edit() {
  sed "$2" "$1" >"$1.tmp" && mv "$1.tmp" "$1"
}

# 3. Skipped tests
for marker in '[Ignore("later")]' '[TestCase(3, 4, IgnoreReason = "later")]' '[Explicit]'; do
  repo=$(feature_pr)
  edit "$repo/tests/CalculatorTests.cs" "s|    \[Test\] // needs: Subtract|    $marker\\
    [Test] // needs: Subtract|"
  git -C "$repo" add -A && git -C "$repo" commit --quiet --amend --no-edit
  expect "added $marker fails" fail "$repo" "adds skipped tests"
done

repo=$(feature_pr)
edit "$repo/tests/CalculatorTests.cs" 's|Subtract_TwoNumbers_ReturnsDifference() { }|Subtract_TwoNumbers_ReturnsDifference() { Assume.That(false); }|'
git -C "$repo" add -A && git -C "$repo" commit --quiet --amend --no-edit
expect "added Assume.That fails" fail "$repo" "adds skipped tests"

# 3b. A skip marker inside a string literal is test data, not a skipped test (issue #380).
repo=$(feature_pr)
cat >> "$repo/tests/CalculatorTests.cs" <<'EOF2'
public class MarkerDataTests
{
    [TestCase("Assert.Ignore(\"x\");")]
    public void Marker_InString_IsData(string source) { }
}
EOF2
git -C "$repo" add -A && git -C "$repo" commit --quiet --amend --no-edit
expect "added [TestCase(\"Assert.Ignore(...)\")] passes" pass "$repo"

repo=$(feature_pr)
edit "$repo/tests/CalculatorTests.cs" 's|Subtract_TwoNumbers_ReturnsDifference() { }|Subtract_TwoNumbers_ReturnsDifference() { Assert.Ignore("x"); }|'
git -C "$repo" add -A && git -C "$repo" commit --quiet --amend --no-edit
expect "added Assert.Ignore(\"x\"); statement fails" fail "$repo" "adds skipped tests"

repo=$(feature_pr)
edit "$repo/tests/CalculatorTests.cs" "s|    \[Test\] // needs: Subtract|    [Ignore(\"x\")]\\
    [Test] // needs: Subtract|"
git -C "$repo" add -A && git -C "$repo" commit --quiet --amend --no-edit
expect "added [Ignore(\"x\")] attribute fails" fail "$repo" "adds skipped tests"

# 4. Removed tests
repo=$(fixture)
edit "$repo/tests/CalculatorTests.cs" '/\[TestCase(2, 3)\]/d'
commit "$repo" "delete a test case"
expect "deleted test fails" fail "$repo" "removes tests"

repo=$(fixture)
edit "$repo/tests/CalculatorTests.cs" 's|    \[Test\] // needs: Add|    // [Test] // needs: Add|'
commit "$repo" "comment out a test"
expect "commented-out [Test] fails" fail "$repo" "removes tests"

# 4b. The run without the change fails before any test runs (issue #49).
# pr_with_runner <repo> <error line>: a feature PR whose test runner prints <error line> and exits 1
# when src/Project.marker is missing, the way `dotnet test` stops when it finds no project to build.
pr_with_runner() {
  local repo=$1 runner
  runner=$(cat "$repo/run-tests.sh")
  {
    echo '#!/usr/bin/env bash'
    echo "[ -f src/Project.marker ] || { echo '$2'; exit 1; }"
    tail -n +2 <<<"$runner"
  } > "$repo/run-tests.sh"
  touch "$repo/src/Project.marker"
  git -C "$repo" add -A && git -C "$repo" commit --quiet --amend --no-edit
}

for error in 'MSBUILD : error MSB1003: Specify a project or solution file.' \
             'MSBUILD : error MSB1009: Project file does not exist.' \
             '/repo/Test.csproj : error NU1101: Unable to find package NUnit.'; do
  repo=$(feature_pr)
  pr_with_runner "$repo" "$error"
  expect "setup failure on base is not red: ${error#*error }" fail "$repo" "failed before any test ran"
done

# A compile error caused by the missing production code is still red.
repo=$(feature_pr)
runner=$(cat "$repo/run-tests.sh")
echo "${runner//echo \"red: \$word\"/echo \"Calc.cs(1,1): error CS0103: The name '\$word' does not exist\"}" > "$repo/run-tests.sh"
git -C "$repo" add -A && git -C "$repo" commit --quiet --amend --no-edit
expect "compile error from missing production code is red" pass "$repo"

# 4c. Annotations (issue #133). setup-dotnet's csc problem matcher turns a line like
# "File.cs(1,1): error CS0103: ..." into an ##[error] annotation. The base run is expected to fail to
# compile, so its output must not match; the head run's must, and the real failure goes to the summary.
csc_line='^[^[:space:]].*\([0-9]+(,[0-9]+)*\): +(error|warning) +[A-Za-z]+[0-9]+:'
run_check() { # run_check <repo> [summary file]: the check's output in $out
  out=$(cd "$1" && BASE_SHA=$(git rev-parse HEAD^) TEST_CMD="bash run-tests.sh" GITHUB_STEP_SUMMARY="${2:-}" "$check" 2>&1)
}
annotation() { # annotation <description> <command...>
  if "${@:2}"; then echo "PASS  $1"; else echo "FAIL  $1"; echo "$out" | sed 's/^/      /'; failures=$((failures + 1)); fi
}
compile_error_runner() { # the fixture's runner, printing a compiler error for each missing word
  local runner; runner=$(cat "$1/run-tests.sh")
  echo "${runner//echo \"red: \$word\"/echo \"Calc.cs(1,1): error CS0103: The name '\$word' does not exist [/repo/Calc.csproj]\"}" > "$1/run-tests.sh"
}

repo=$(feature_pr)
compile_error_runner "$repo"
git -C "$repo" add -A && git -C "$repo" commit --quiet --amend --no-edit
run_check "$repo"
annotation "expected compile errors of the base run are not annotations" bash -c '! grep -qE "$1" <<<"$2"' _ "$csc_line" "$out"
annotation "the base run's output is still in the log" grep -qF "| Calc.cs(1,1): error CS0103: The name" <<<"$out"

repo=$(feature_pr)
compile_error_runner "$repo"
echo 'public static class Minus { }' > "$repo/src/Subtractor.cs" # head still lacks Subtract
git -C "$repo" add -A && git -C "$repo" commit --quiet --amend --no-edit
summary="$tmp_root/summary.md"
run_check "$repo" "$summary"
annotation "compile errors of the head run stay annotations" grep -qE "$csc_line" <<<"$out"
annotation "the real failure goes to the job summary" grep -qF "the tests fail with the change" "$summary"

# 4d. Label events (issue #136). A label other than "refactor" doesn't change what tdd-check checks, so the
# check reports the last completed tdd-check result on the PR's head commit again, read with `gh api`, and
# doesn't run the suite. Without such a result, or while another tdd-check run is still going, it runs in full.
stub_bin="$tmp_root/stub-bin"
mkdir -p "$stub_bin"
printf '%s\n' '#!/usr/bin/env bash' 'printf "%s\n" "$STUB_CHECK_RUNS"' > "$stub_bin/gh"
chmod +x "$stub_bin/gh"
runs() { # runs <status:conclusion>...: a check-runs answer, oldest first, with this run itself last (in progress)
  local items="" n=0 pair
  for pair in "$@" "in_progress:"; do
    n=$((n + 1))
    items+="${items:+,}{\"name\":\"tdd-check\",\"status\":\"${pair%%:*}\",\"conclusion\":\"${pair#*:}\",\"completed_at\":\"2026-11-24T10:0$n:00Z\"}"
  done
  echo "{\"check_runs\":[$items]}"
}
label_event() { # label_event <description> <pass|fail> <repo> <action> <label> <check runs> <text the output must contain>
  local out status
  out=$(cd "$3" && rm -f suite-ran && BASE_SHA=$(git rev-parse HEAD^) TEST_CMD="bash run-tests-marked.sh" \
    EVENT_ACTION="$4" EVENT_LABEL="$5" PR_HEAD_SHA=abc123 GITHUB_REPOSITORY=example/repo STUB_CHECK_RUNS="$6" \
    PATH="$stub_bin:$PATH" "$check" 2>&1)
  status=$?
  if { [ "$2" = pass ] && [ "$status" -eq 0 ]; } || { [ "$2" = fail ] && [ "$status" -ne 0 ]; }; then
    if grep -qF -- "$7" <<<"$out"; then echo "PASS  $1"; return; fi
  fi
  echo "FAIL  $1: expected $2 with '$7', got exit $status"
  echo "$out" | sed 's/^/      /'
  failures=$((failures + 1))
}
marked() { # marked <repo>: the fixture's runner also leaves suite-ran behind, so a case can tell whether the suite ran
  printf '%s\n' 'touch suite-ran' 'exec bash run-tests.sh "$@"' > "$1/run-tests-marked.sh"
  git -C "$1" add -A && git -C "$1" commit --quiet --amend --no-edit
  echo "$1"
}
ran() { if [ -f "$2/suite-ran" ]; then echo "PASS  $1"; else echo "FAIL  $1"; failures=$((failures + 1)); fi; }
not_ran() { if [ -f "$2/suite-ran" ]; then echo "FAIL  $1"; failures=$((failures + 1)); else echo "PASS  $1"; fi; }

# A refactor PR without the label fails a full run, so a pass here can only be the reused result.
repo=$(marked "$(refactor_pr)")
label_event "a label event reports an earlier success again" pass "$repo" labeled documentation "$(runs completed:success)" "reports the last tdd-check result on this commit again"
not_ran "a reused success doesn't run the suite" "$repo"

repo=$(marked "$(feature_pr)")
label_event "a label event reports an earlier failure again" fail "$repo" unlabeled documentation "$(runs completed:success completed:failure)" "the last tdd-check run on this commit failed"
not_ran "a reused failure doesn't run the suite" "$repo"

repo=$(marked "$(feature_pr)")
label_event "a cancelled run isn't a result to report" pass "$repo" labeled documentation "$(runs completed:cancelled)" "no earlier tdd-check result on this commit"
ran "without an earlier result the suite runs" "$repo"

repo=$(marked "$(feature_pr)")
label_event "another run still going means a full run" pass "$repo" labeled documentation "$(runs completed:failure in_progress:)" "another tdd-check run on this commit is still going"
ran "while another run is going the suite runs" "$repo"

repo=$(marked "$(refactor_pr)")
label_event "the refactor label still runs in full" fail "$repo" labeled refactor "$(runs completed:success)" "no test under tests/ was added or changed"
ran "the refactor label runs the suite" "$repo"

# 5. The workflow (finding 1 and 3)
wf() { grep -qE "$2" "$workflow" && echo "PASS  $1" || { echo "FAIL  $1: '$2' not in $workflow"; failures=$((failures + 1)); }; }
wf "workflow re-runs on label changes" 'types: \[opened, synchronize, reopened, labeled, unlabeled\]'
wf "workflow takes BASE_SHA from the merge commit's first parent" 'BASE_SHA=\$\(git rev-parse HEAD\^1\)'
wf "workflow passes the PR's labels" 'PR_LABELS: \$\{\{ join\(github\.event\.pull_request\.labels\.\*\.name, '"','"'\) \}\}'
wf "workflow checks tools/Analyzers (issue #49): SRC_DIR" 'SRC_DIR: tools/Analyzers/Team\.Analyzers/$'
wf "workflow checks tools/Analyzers (issue #49): TEST_DIR" 'TEST_DIR: tools/Analyzers/Team\.Analyzers\.Tests/$'
wf "workflow passes the event and its label (issue #136)" 'EVENT_LABEL: \$\{\{ github\.event\.label\.name \}\}'
wf "workflow can read the earlier check runs (issue #136)" 'checks: read'
wf "workflow has a concurrency group per PR (issue #136)" 'group: \$\{\{ github\.workflow \}\}-\$\{\{ github\.event\.pull_request\.number \}\}'
wf "a label run never cancels a run in progress (issue #136)" "cancel-in-progress: \\\$\\{\\{ github\\.event\\.action != 'labeled' && github\\.event\\.action != 'unlabeled' \\}\\}"
api_workflow="$here/../../.github/workflows/api-tests.yml"
if grep -qF "cancel-in-progress: \${{ github.event_name == 'pull_request' }}" "$api_workflow"; then
  echo "PASS  api-tests.yml cancels in-progress runs for pull requests only (issue #136)"
else
  echo "FAIL  api-tests.yml cancels in-progress runs for pull requests only (issue #136)"; failures=$((failures + 1))
fi
if grep -qF 'pull_request.base.sha' "$workflow"; then
  echo "FAIL  workflow no longer uses pull_request.base.sha"; failures=$((failures + 1))
else
  echo "PASS  workflow no longer uses pull_request.base.sha"
fi

echo
if (( failures == 0 )); then echo "All tdd-check tests pass."; else echo "$failures tdd-check test(s) failed."; fi
exit $(( failures > 0 ))
