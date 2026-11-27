#!/usr/bin/env bash
# Prints a prefilled PR body for <issue> that passes the pr-description check (issue #523), so a PR starts
# from the template instead of a hand copy. `gh pr create --fill` and `--body` skip the template (AGENTS.md).
# Usage: tools/dev/pr-body.sh <issue> > body.md && gh pr create --base develop --body-file body.md
# The body is .github/pull_request_template.md read as it is now, with these changes:
#   - "Closes #" gets the issue number
#   - "What changed and why" gets a TODO line for the author to replace
#   - "No existing test was changed or removed" is ticked; untick it and say which test changed, if one did
#   - the Domain section is left out unless the branch changes a file under src/*/Domain
# Every other heading and line of the template is kept, so a section added to it shows up here too.
# PR_BODY_TEMPLATE names another template, PR_BODY_CHANGED_FILES a file listing the changed files (one per line);
# by default the changed files are `git diff --name-only origin/develop...HEAD`. Tests: tools/dev/pr-body.test.sh
set -uo pipefail

issue=${1-}
if ! [[ $issue =~ ^[0-9]+$ ]]; then
  echo "usage: tools/dev/pr-body.sh <issue number>" >&2
  exit 2
fi

root=$(git -C "$(dirname "$0")" rev-parse --show-toplevel) || exit 2
template=${PR_BODY_TEMPLATE:-$root/.github/pull_request_template.md}
[ -f "$template" ] || {
  echo "error: no PR template at $template" >&2
  exit 2
}

if [ -n "${PR_BODY_CHANGED_FILES-}" ]; then
  changed=$(cat "$PR_BODY_CHANGED_FILES")
else
  changed=$(git -C "$root" diff --name-only origin/develop...HEAD 2>/dev/null)
fi
domain=0
grep -qE '^src/[^/]+/Domain/' <<<"$changed" && domain=1

awk -v issue="$issue" -v domain="$domain" '
  { line = $0; sub(/\r$/, "", line) }
  /^## / { section = line; skip = (line ~ /^## Domain/ && domain == 0) }
  skip { next }
  line == "Closes #" { print "Closes #" issue; next }
  line ~ /^- \[ \] No existing test was changed or removed/ { sub(/\[ \]/, "[x]", line); print line; next }
  section == "## What changed and why" && line ~ /^<!--.*-->$/ { print "TODO: what changed and why, in one or two lines."; next }
  { print line }
' "$template"
