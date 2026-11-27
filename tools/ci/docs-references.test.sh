#!/usr/bin/env bash
# No file points at the moved planning docs any more (#516): references go to docs/TECHNICAL_REFERENCE.md and
# METHODOLOGY section 11. Only the history in docs/legacy/ and the checks that name the files may still say them.
# Usage: tools/ci/docs-references.test.sh   (from anywhere in the repo; exit 0 = all pass)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2

failures=0
for name in CSHARP_SETUP_TODO DDD_PLAN; do
  found=$(git grep -l "$name" -- ':!docs/legacy/' ':!tools/ci/docs-references.test.sh' ':!tools/ci/docs-legacy.test.sh')
  if [ -z "$found" ]; then
    echo "PASS  no file outside docs/legacy/ names $name"
  else
    echo "FAIL  these files still name $name; point them at docs/TECHNICAL_REFERENCE.md (or METHODOLOGY section 11):"
    sed 's/^/      /' <<<"$found"
    failures=$((failures + 1))
  fi
done

for stub in docs/CSHARP_SETUP_TODO.md docs/DDD_PLAN.md; do
  if [ -e "$stub" ]; then
    echo "FAIL  the stub $stub is still there: delete it once nothing points at it"
    failures=$((failures + 1))
  else
    echo "PASS  the stub $stub is gone"
  fi
done

[ "$failures" -eq 0 ] || exit 1
