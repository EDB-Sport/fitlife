#!/usr/bin/env bash
# The docs layout of issue #515: docs/TECHNICAL_REFERENCE.md says what is built, the planning docs are history in
# docs/legacy/ with a warning banner, and a stub at each old path keeps its links working until #516 repoints them.
# Usage: tools/ci/docs-legacy.test.sh   (from anywhere in the repo; exit 0 = all pass)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2

failures=0
pass() { echo "PASS  $1"; }
fail() { echo "FAIL  $1"; failures=$((failures + 1)); }

reference=docs/TECHNICAL_REFERENCE.md
if [ -f "$reference" ]; then
  pass "$reference exists"
  # The section numbers of the old TODO stay, so "section N" pointers keep their meaning
  for section in 0 1 2 3 4 5 6 6a 7 8 9 9a 10 11; do
    if grep -qE "^## ${section}\. " "$reference"; then
      pass "$reference has section $section"
    else
      fail "$reference has no heading '## $section. ...': keep the section numbers of CSHARP_SETUP_TODO.md"
    fi
  done
  if grep -qE '^\s*- \[[ xX]\]' "$reference"; then
    fail "$reference has checkboxes: it says what is built, not what to do"
  else
    pass "$reference has no checkboxes"
  fi
else
  fail "$reference is missing (#515)"
fi

for legacy in docs/legacy/CSHARP_SETUP_TODO.md docs/legacy/DDD_PLAN.md docs/legacy/GLOSSARY.md docs/legacy/CONTEXT-MAP.md; do
  if [ ! -f "$legacy" ]; then
    fail "$legacy is missing: git mv the planning doc into docs/legacy/"
  elif head -n 3 "$legacy" | grep -q 'OUTDATED: historical record only'; then
    pass "$legacy starts with the banner"
  else
    fail "$legacy doesn't start with the OUTDATED banner (#515)"
  fi
done

if [ -f docs/legacy/README.md ]; then
  pass "docs/legacy/README.md exists"
else
  fail "docs/legacy/README.md is missing: say what the folder holds and that it is history"
fi

# The stubs: short, and pointing at the replacement
for stub in docs/CSHARP_SETUP_TODO.md docs/DDD_PLAN.md; do
  if [ -f "$stub" ] && [ "$(wc -l <"$stub")" -le 5 ] && grep -q 'Moved' "$stub"; then
    pass "$stub is a short 'Moved' stub"
  else
    fail "$stub is not a stub of at most 5 lines that says 'Moved'"
  fi
done

if grep -q 'docs/legacy/' AGENTS.md; then
  pass "AGENTS.md says docs/legacy/ is history"
else
  fail "AGENTS.md doesn't mention docs/legacy/"
fi

[ "$failures" -eq 0 ] || exit 1
