#!/usr/bin/env bash
# Runs one CI step and, if it fails, writes a short summary to the run's summary page (issue #132): the failing
# check, the first real error line and a one-line hint, and prints the error as an annotation, which
# `gh run view` lists. Read that before the full log (AGENTS.md): one failed assertion can mean a 169 KB log.
# A passing step adds nothing. The step's output and exit code don't change.
#
# Usage: tools/ci/summarize.sh <check> <hint> -- <command> [args...]
# Tests: tools/ci/summarize.test.sh
set -uo pipefail

if [ "$#" -lt 4 ] || [ "$3" != "--" ]; then
  echo "Usage: $0 <check> <hint> -- <command> [args...]" >&2
  exit 64
fi
check=$1 hint=$2
shift 3

output=$(mktemp)
trap 'rm -f "$output"' EXIT
"$@" 2>&1 | tee "$output"
status=${PIPESTATUS[0]}

if [ "$status" -ne 0 ]; then
  # Compiler and format errors (file(line,col): error XX0000), failed NUnit tests, Newman assertions
  error_line='(: error [A-Z]+[0-9]*:|^[[:space:]]*Failed [A-Za-z_]|AssertionError|^[[:space:]]*error[ :])'
  first=$(grep -m1 -E "$error_line" "$output" || true)
  if [ -z "$first" ]; then
    first=$(grep -v '^[[:space:]]*$' "$output" | tail -n 1 || true)
  fi
  first=$(printf '%s' "$first" | sed 's/^[[:space:]]*//' | cut -c1-400)
  # An annotation too: gh run view lists annotations but never shows the summary page (AGENTS.md).
  # % is escaped first, then CR and LF, as the workflow-command format requires.
  annotation=${first//%/%25}
  annotation=${annotation//$'\r'/%0D}
  annotation=${annotation//$'\n'/%0A}
  echo "::error title=$check failed::$annotation"
  [ -n "${GITHUB_STEP_SUMMARY:-}" ] || exit "$status"
  {
    echo "### \`$check\` failed (exit $status)"
    echo
    echo '```text'
    echo "$first"
    echo '```'
    echo
    echo "**Hint:** $hint"
    echo
  } >> "$GITHUB_STEP_SUMMARY"
fi
exit "$status"
