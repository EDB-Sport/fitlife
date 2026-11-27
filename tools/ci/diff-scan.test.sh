#!/usr/bin/env bash
# Tests for tools/ci/diff-scan.sh (issue #5, docs/TECHNICAL_REFERENCE.md section 10).
# Each case builds a throwaway git repo: a base commit, then a head commit with the change.
# Usage: tools/ci/diff-scan.test.sh   (exit code 0 = all cases pass)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

scan="$(cd "$(dirname "$0")" && pwd)/diff-scan.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# new_repo: a fresh repo with a base commit holding a root .globalconfig, a test and a workflow
new_repo() {
  rm -rf "$work/repo"
  mkdir -p "$work/repo"
  cd "$work/repo" || exit 1
  git init -q
  git config user.email ci@example.invalid
  git config user.name ci
  mkdir -p tests/A.Tests .github/workflows src/A
  printf 'is_global = true\n' > .globalconfig
  printf 'public class ATests\n{\n    public void Works() { }\n}\n' > tests/A.Tests/ATests.cs
  printf 'jobs:\n  build:\n    runs-on: ubuntu-latest\n' > .github/workflows/ci.yml
  printf 'public class A { }\n' > src/A/A.cs
  git add -A
  git commit -qm base
  base=$(git rev-parse HEAD)
}

# commit_head: commits the working tree as the head commit
commit_head() {
  git add -A
  git commit -qm head
}

# expect <description> <pass|fail> [text the output must contain] [text the output must not contain]
expect() {
  local out status
  out=$(BASE_SHA="$base" HEAD_SHA=HEAD "$scan" 2>&1)
  status=$?
  if { [ "$2" = pass ] && [ "$status" -eq 0 ]; } || { [ "$2" = fail ] && [ "$status" -ne 0 ]; }; then
    if grep -qF -- "${3:-}" <<<"$out" && { [ -z "${4:-}" ] || ! grep -qF -- "$4" <<<"$out"; }; then
      echo "PASS  $1"
      return
    fi
  fi
  echo "FAIL  $1: expected $2${3:+ with '$3'}${4:+ without '$4'}, got exit $status"
  echo "$out" | sed 's/^/      /'
  failures=$((failures + 1))
}

# 1. A clean change: new code and a new test, nothing removed under tests/
new_repo
printf 'public class B { }\n' > src/A/B.cs
printf 'public class BTests { }\n' > tests/A.Tests/BTests.cs
commit_head
expect "clean change passes without labels" pass "" "label:"

# 2. [ExcludeFromCodeCoverage] on an added line fails
new_repo
printf '[ExcludeFromCodeCoverage]\npublic class B { }\n' > src/A/B.cs
commit_head
expect "added [ExcludeFromCodeCoverage] fails" fail "ExcludeFromCodeCoverage"

# 3. The fully qualified attribute, with a justification, fails too
new_repo
printf '[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "x")]\npublic class B { }\n' > src/A/B.cs
commit_head
expect "qualified ExcludeFromCodeCoverage fails" fail "ExcludeFromCodeCoverage"

# 4. Removing an existing [ExcludeFromCodeCoverage] is fine: only added lines count
new_repo
printf '[ExcludeFromCodeCoverage]\npublic class C { }\n' > src/A/C.cs
git add -A && git commit -qm "old exclusion" && base=$(git rev-parse HEAD)
printf 'public class C { }\n' > src/A/C.cs
commit_head
expect "removing ExcludeFromCodeCoverage passes" pass

# 5. Mentioning the attribute in docs is not code
new_repo
mkdir -p docs && printf 'Never use [ExcludeFromCodeCoverage].\n' > docs/rules.md
commit_head
expect "ExcludeFromCodeCoverage in a .md file passes" pass

# 6. A new .editorconfig below the root fails
new_repo
printf '[*.cs]\ndotnet_diagnostic.CA1062.severity = none\n' > src/A/.editorconfig
commit_head
expect "nested .editorconfig fails" fail "src/A/.editorconfig"

# 7. A new .globalconfig below the root fails, whatever its name
new_repo
printf 'is_global = true\n' > tests/A.Tests/relax.globalconfig
commit_head
expect "nested .globalconfig fails" fail "tests/A.Tests/relax.globalconfig"

# 8. A new .editorconfig at the root is allowed
new_repo
printf 'root = true\n' > .editorconfig
commit_head
expect "root .editorconfig passes" pass

# 9. Editing the root .globalconfig is allowed
new_repo
printf 'is_global = true\ndotnet_diagnostic.CA1000.severity = error\n' > .globalconfig
commit_head
expect "editing the root .globalconfig passes" pass

# 10. continue-on-error: true in a workflow fails
new_repo
printf '    continue-on-error: true\n' >> .github/workflows/ci.yml
commit_head
expect "continue-on-error: true in a workflow fails" fail "continue-on-error"

# 11. if: false in a workflow fails
new_repo
printf '    if: false\n' >> .github/workflows/ci.yml
commit_head
expect "if: false in a workflow fails" fail "if: false"

# 12. if: ${{ false }} is the same escape
new_repo
printf '    if: ${{ false }}\n' >> .github/workflows/ci.yml
commit_head
expect "if: \${{ false }} in a workflow fails" fail "if:"

# 13. A real condition in a workflow is fine
new_repo
printf "    if: github.event_name == 'pull_request'\n" >> .github/workflows/ci.yml
commit_head
expect "a real if: condition passes" pass

# 14. continue-on-error: true outside .github/workflows/ is not checked
new_repo
mkdir -p docs && printf 'continue-on-error: true\n' > docs/ci.md
commit_head
expect "continue-on-error outside workflows passes" pass

# 15. Changing an existing line under tests/ adds tests-modified
new_repo
sed 's/Works/StillWorks/' tests/A.Tests/ATests.cs >ATests.tmp && mv ATests.tmp tests/A.Tests/ATests.cs # not sed -i: GNU and BSD differ (#87)
commit_head
expect "changed test line adds tests-modified" pass "label: tests-modified"

# 16. Deleting a test file adds tests-modified
new_repo
git rm -q tests/A.Tests/ATests.cs
commit_head
expect "deleted test file adds tests-modified" pass "label: tests-modified"

# 17. Only adding lines under tests/ adds no label
new_repo
printf '// another test later\n' >> tests/A.Tests/ATests.cs
commit_head
expect "added test lines add no label" pass "" "tests-modified"

# 18. Any change under contracts/ adds contract-change
new_repo
mkdir -p contracts/booking && printf 'openapi: 3.1.0\n' > contracts/booking/openapi.yaml
commit_head
expect "new contract file adds contract-change" pass "label: contract-change"

# 19. Rule failures and labels are reported together
new_repo
sed 's/Works/StillWorks/' tests/A.Tests/ATests.cs >ATests.tmp && mv ATests.tmp tests/A.Tests/ATests.cs # not sed -i: GNU and BSD differ (#87)
printf '    if: false\n' >> .github/workflows/ci.yml
commit_head
expect "a failing scan still reports its labels" fail "label: tests-modified"

# 20. Only the PR's own changes count: a change on the base branch after the fork point is ignored
new_repo
git switch -qc feature
printf 'public class B { }\n' > src/A/B.cs
commit_head
git switch -q -
printf '[ExcludeFromCodeCoverage]\npublic class D { }\n' > src/A/D.cs
commit_head
base=$(git rev-parse HEAD)
git switch -q feature
expect "changes on the base branch are not scanned" pass

# 21. Without BASE_SHA the script refuses to guess
new_repo
out=$("$scan" 2>&1)
if [ $? -ne 0 ] && grep -qF "BASE_SHA" <<<"$out"; then
  echo "PASS  missing BASE_SHA fails"
else
  echo "FAIL  missing BASE_SHA fails: got '$out'"
  failures=$((failures + 1))
fi

[ "$failures" -eq 0 ] || exit 1
