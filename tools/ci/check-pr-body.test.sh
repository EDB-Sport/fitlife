#!/usr/bin/env bash
# Tests for tools/ci/check-pr-body.sh (issue #34).
# Usage: tools/ci/check-pr-body.test.sh   (exit code 0 = all cases pass)
set -uo pipefail

check="$(dirname "$0")/check-pr-body.sh"
failures=0

# expect <description> <pass|fail> [text the output must contain]   (PR body on stdin)
expect() {
  local out status
  out=$(PR_BODY="$(cat)" "$check" 2>&1)
  status=$?
  if [ "$2" = pass ] && [ "$status" -eq 0 ]; then
    echo "PASS  $1"
  elif [ "$2" = fail ] && [ "$status" -ne 0 ] && grep -qF "${3:-}" <<<"$out"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1: expected $2${3:+ with '$3'}, got exit $status"
    echo "$out" | sed 's/^/      /'
    failures=$((failures + 1))
  fi
}

template=$(cat "$(dirname "$0")/../../.github/pull_request_template.md")

# 1. The untouched template (PR #32) fails on all three rules
expect "untouched template fails: issue" fail "no linked issue" <<<"$template"
expect "untouched template fails: what changed" fail "empty 'What changed and why'" <<<"$template"
expect "untouched template fails: tests" fail "no answer about existing tests" <<<"$template"

# 2. A filled-in body (like PR #11) passes
expect "filled-in body passes" pass <<'EOF'
Closes #3

## What changed and why
<!-- One or two lines. -->
The .NET repo foundation and strict build rules.

## Tests
- [x] The change started with a failing test (or executable acceptance check)
- [x] No existing test was changed or removed
<!-- If an existing test was changed or removed, say which and why. -->
EOF

# 3. Documentation-only (like PR #33): first box n/a, second box ticked
expect "documentation-only body passes" pass <<'EOF'
Closes #2

## What changed and why
A first strategic domain model.

## Tests
- [ ] The change started with a failing test (or executable acceptance check) — n/a, documentation only
- [x] No existing test was changed or removed
EOF

# 4. An existing test changed, with the reason given instead of the tick
expect "changed test with a reason passes" pass <<'EOF'
Fixes #12

## What changed and why
Seats can no longer go below zero.

## Tests
- [x] The change started with a failing test (or executable acceptance check)
- [ ] No existing test was changed or removed

ReservationTests.Reserve_FullSession was renamed: it tested the old error message.
EOF

# 5. "Closes #" without a number is not a link
expect "Closes # without a number fails" fail "no linked issue" <<'EOF'
Closes #

## What changed and why
Something.

## Tests
- [x] No existing test was changed or removed
EOF

# 6. Resolves and lower case are accepted
expect "resolves #n in lower case passes" pass <<'EOF'
resolves #7

## What changed and why
Something.

## Tests
- [x] No existing test was changed or removed
EOF

# 7. Only a template comment under "What changed and why" counts as empty, also with Windows line endings
expect "comment-only section with CRLF fails" fail "empty 'What changed and why'" < <(printf 'Closes #5\r\n\r\n## What changed and why\r\n<!-- One or two lines. -->\r\n\r\n## Tests\r\n- [x] No existing test was changed or removed\r\n')

# 8. A body with no sections at all fails
expect "missing sections fail" fail "empty 'What changed and why'" <<<"Closes #5"

# A body that is fine apart from its first line
rest=$'\n## What changed and why\nSomething.\n\n## Tests\n- [x] No existing test was changed or removed'

# 9. A partial PR links its issue with Refs, without closing it (PRs #40, #42, #44)
expect "Refs #n passes" pass <<<"Refs #9$rest"

# 10. The other keywords and forms GitHub links an issue with
expect "Fixed #n passes" pass <<<"Fixed #12$rest"
expect "Closes owner/repo#n passes" pass <<<"Closes EDB-Sport/project#12$rest"
expect "Closes <issue URL> passes" pass <<<"Closes https://github.com/EDB-Sport/project/issues/12$rest"

# 11. Release and hotfix PRs into main have no issue to close (METHODOLOGY section 2.1)
HEAD_REF=release/1.0.0 expect "release PR without an issue passes" pass <<<"$rest"
HEAD_REF=hotfix/bookingservice-1.0.1 expect "hotfix PR without an issue passes" pass <<<"$rest"
HEAD_REF=feature/12-reserve expect "feature PR without an issue fails" fail "no linked issue" <<<"$rest"

# 12. With the box unticked, the text has to say which test changed, not just that tests pass
expect "unticked box with unrelated text fails" fail "no answer about existing tests" <<'EOF'
Closes #5

## What changed and why
Something.

## Tests
- [x] The change started with a failing test (or executable acceptance check)
- [ ] No existing test was changed or removed

All tests pass.
EOF

# 13. Only the exact heading counts: "## Testsuite" is not "## Tests"
expect "a heading that starts with Tests fails" fail "no answer about existing tests" <<'EOF'
Closes #5

## What changed and why
Something.

## Testsuite
- [x] No existing test was changed or removed
EOF

# 14. Dependabot PRs are opened by a bot: no issue, no template (issue #281). Only that author is exempt.
dependabot_body=$'Bumps the github-actions group with 1 update.\n\nUpdates `actions/checkout` from 4.4.0 to 5.0.0'
PR_AUTHOR='dependabot[bot]' expect "Dependabot PR without the template passes" pass <<<"$dependabot_body"
PR_AUTHOR='someone' expect "same body from a person still fails" fail "no linked issue" <<<"$dependabot_body"

# 15. The template's Tests hint is a visible line, not an HTML comment (issue #481): CI strips comments and GitHub hides them
hint_line=$(grep -F 'If an existing test was changed or removed' "$(dirname "$0")/../../.github/pull_request_template.md")
if [ -n "$hint_line" ] && [[ "$hint_line" != '<!--'* ]]; then
  echo "PASS  the template's Tests hint is visible, not an HTML comment"
else
  echo "FAIL  the template's Tests hint is missing or inside an HTML comment: $hint_line"
  failures=$((failures + 1))
fi

echo
if [ "$failures" -eq 0 ]; then
  echo "All PR description checks work."
else
  echo "$failures case(s) failed."
fi
exit "$failures"
