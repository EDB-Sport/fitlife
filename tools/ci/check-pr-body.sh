#!/usr/bin/env bash
# Checks that a PR description follows .github/pull_request_template.md (docs/METHODOLOGY.md section 2).
# Usage: PR_BODY="<description>" tools/ci/check-pr-body.sh   (or pipe the description in on stdin)
# Exit code = number of rules broken. Tests: tools/ci/check-pr-body.test.sh
set -uo pipefail

body=${PR_BODY-$(cat)}
# Template hints in <!-- --> don't count as text; Windows line endings don't matter
body=$(perl -0pe 's/<!--.*?-->//gs; s/\r//g' <<<"$body")

# section <heading>: the lines under "## <heading>", up to the next "## " heading
section() {
  awk -v heading="## $1" 'index($0, heading) == 1 { inside = 1; next } /^## / { inside = 0 } inside' <<<"$body"
}

# has_text: true when stdin has anything but whitespace
has_text() {
  [ -n "$(tr -d '[:space:]')" ]
}

errors=0
fail() {
  echo "::error title=PR description::$1"
  errors=$((errors + 1))
}

if ! grep -qiE '\b(closes|fixes|resolves) #[0-9]+' <<<"$body"; then
  fail "Found no linked issue. Write 'Closes #<issue number>' on the first line of the PR description, so the PR links to its issue."
fi

if ! section 'What changed and why' | has_text; then
  fail "Found an empty 'What changed and why' section. Write one or two lines under that heading in the PR description: what changed, and why."
fi

tests=$(section 'Tests')
if ! grep -qiE '^\s*- \[x\] No existing test was changed or removed' <<<"$tests" \
  && ! grep -vE '^\s*- \[[ xX]\]' <<<"$tests" | has_text; then
  fail "Found no answer about existing tests. In the 'Tests' section of the PR description, tick 'No existing test was changed or removed', or say which test changed and why."
fi

if [ "$errors" -eq 0 ]; then
  echo "The PR description follows the template."
else
  echo "Edit the PR description to fix this; the check runs again when you save. Rules: docs/PRrules.md"
fi
exit "$errors"
