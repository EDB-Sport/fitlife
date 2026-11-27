#!/usr/bin/env bash
# Tests tools/dev/pr-body.sh on fixture input, with no network (issue #523): the body it prints passes
# tools/ci/check-pr-body.sh and follows .github/pull_request_template.md.
# Usage: tools/dev/pr-body.test.sh   (from the repo root, or anywhere in the repo; exit 0 = pass)
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2
script=tools/dev/pr-body.sh
template=.github/pull_request_template.md
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
failures=0

printf '%s\n' docs/README.md tools/dev/pr-body.sh >"$tmp/no-domain.txt"
printf '%s\n' src/BookingService/Domain/ClassSession.cs tests/BookingService.Tests/ClassSessionTests.cs >"$tmp/domain.txt"

# run <changed files fixture> [args...]: runs the script, sets $out, $err and $code.
run() {
  local changed=$1
  shift
  out=$(PR_BODY_CHANGED_FILES="$changed" PR_BODY_TEMPLATE="${RUN_TEMPLATE:-$template}" "$script" "$@" 2>"$tmp/err")
  code=$?
  err=$(cat "$tmp/err")
}

expect() { # expect <label> <condition exit status>
  if [ "$2" -eq 0 ]; then
    echo "PASS  $1"
  else
    echo "FAIL  $1"
    sed 's/^/      out: /' <<<"$out"
    sed 's/^/      err: /' <<<"$err"
    failures=$((failures + 1))
  fi
}

has() { grep -qxF -- "$1" <<<"$out"; }

run "$tmp/no-domain.txt" 523
[ "$code" -eq 0 ] && [ "$(head -1 <<<"$out")" = "Closes #523" ]
expect "the body starts with 'Closes #<issue>'" $?

missing=$(grep -E '^## ' "$template" | grep -v '^## Domain' | while IFS= read -r heading; do has "$heading" || echo "$heading"; done)
[ -z "$missing" ]
expect "the body has every heading of the template (Domain aside)" $?

PR_BODY="$out" HEAD_REF=feature/523-pr-body tools/ci/check-pr-body.sh >/dev/null
expect "the body passes tools/ci/check-pr-body.sh" $?

has "- [x] No existing test was changed or removed"
expect "the 'No existing test was changed or removed' box is ticked" $?

has "- [ ] The change started with a failing test (or executable acceptance check)"
expect "the 'started with a failing test' box is left for the author" $?

! grep -qF '## Domain' <<<"$out"
expect "no file under src/*/Domain changed: the Domain checklist is left out" $?

run "$tmp/domain.txt" 523
missing=$(sed -n '/^## Domain/,/^## /p' "$template" | grep -E '^(## Domain|- \[ \])' | while IFS= read -r line; do has "$line" || echo "$line"; done)
[ "$code" -eq 0 ] && [ -z "$missing" ]
expect "a file under src/*/Domain changed: the Domain checklist is included" $?

# The headings come from the template file: a heading added there shows up in the body.
awk '$0 == "## Tests" { print "## What to verify and how"; print "" } { print }' "$template" >"$tmp/template.md"
RUN_TEMPLATE="$tmp/template.md" run "$tmp/no-domain.txt" 523
[ "$code" -eq 0 ] && has "## What to verify and how"
expect "a section added to the template (What to verify and how, #520) is in the body" $?

run "$tmp/no-domain.txt"
[ "$code" -ne 0 ] && grep -qi usage <<<"$err"
expect "no issue: a usage line and a non-zero exit" $?

run "$tmp/no-domain.txt" abc
[ "$code" -ne 0 ] && grep -qi usage <<<"$err"
expect "a non-numeric issue: a usage line and a non-zero exit" $?

if [ "$failures" -eq 0 ]; then echo "All pr-body checks passed."; else echo "$failures check(s) failed."; fi
exit $((failures > 0))
