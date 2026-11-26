#!/usr/bin/env bash
# Checks .github/CODEOWNERS for the rule of issue #327: .hadolint.yaml is code-owned by the team.
# Usage: tools/ci/codeowners.test.sh   (from the repo root, or anywhere in the repo; exit 0 = pass)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2
file=.github/CODEOWNERS

if grep -Eq '^/\.hadolint\.yaml[[:space:]]+@EDB-Sport/edb-team[[:space:]]*$' "$file"; then
  echo "PASS  .hadolint.yaml is code-owned by @EDB-Sport/edb-team"
  exit 0
fi

echo "FAIL  $file has no line '/.hadolint.yaml @EDB-Sport/edb-team' (issue #327)"
exit 1
