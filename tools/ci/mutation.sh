#!/usr/bin/env bash
# Mutation testing on the C# code a PR changes (issue #53, docs/TECHNICAL_REFERENCE.md section 10, the mutation job).
# Runs the dotnet-stryker pinned in dotnet-tools.json. stryker-config.json (code-owned) holds the solution,
# the reporters and the break threshold: a mutation score below it exits non-zero.
#
# The changed lines go to --mutate as "path{start..end}" spans (#382), not to Stryker's own --since: --since mutates
# every mutant in a changed file (and in solution mode Stryker 5.1 counts every changed file as a test file and
# filters out the mutants of a changed source file).
#   - The numbers of a span are character offsets, not line numbers (the end is exclusive).
#   - A mutant counts only if its whole code is inside a span: a changed line in the middle of a multi-line
#     expression doesn't mutate that whole expression.
#   - A file whose only change is comments or whitespace (code-lines.sh, as in tdd-check) is not mutated.
#   - A file whose code changed only by removing lines has no changed line, so all of it is mutated.
#
# Usage: SINCE=<git ref to compare with> tools/ci/mutation.sh   (in CI: SINCE=origin/<base branch>)
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/code-lines.sh"

: "${SINCE:?SINCE must be the git ref to compare with, e.g. origin/develop}"

changed=$(git diff --name-only --diff-filter=d "$SINCE"...HEAD -- '*.cs')
if [ -z "$changed" ]; then
  echo "No C# files changed since $SINCE, so there is nothing to mutate."
  exit 0
fi

base=$(git merge-base "$SINCE" HEAD)

# The code lines of a version of a file without their indentation. code_changed compares base and head with it.
trimmed_code() { git show "$1" 2>/dev/null | code_lines | sed 's/^[[:space:]]*//; s/[[:space:]]*$//'; }

# Did more than comments and whitespace change? Inside a multi-line string they are code (#484).
code_changed() {
  has_multiline_string "$base" "$1" || has_multiline_string HEAD "$1" ||
    ! cmp -s <(trimmed_code "$base:$1") <(trimmed_code "HEAD:$1")
}

# The {start..end} spans of the lines a file's diff adds or edits, counted as Stryker (Roslyn) counts them:
# UTF-16 characters, without a byte order mark. awk reads bytes (LC_ALL=C): UTF-8 continuation bytes don't count,
# and a four-byte character counts twice.
spans() {
  git diff -U0 --no-color --no-ext-diff "$base" -- "$1" | LC_ALL=C awk '
    NR == FNR {
      if (/^@@ /) {
        split($3, hunk, ","); first = substr(hunk[1], 2) + 0; count = (hunk[2] == "") ? 1 : hunk[2] + 0
        if (count) { from[first]; to[first + count - 1] }
      }
      next
    }
    FNR == 1 { sub(/^\357\273\277/, "") }
    FNR in from { start = position }
    { wide = gsub(/[\360-\364]/, "&"); gsub(/[\200-\277]/, ""); position += length($0) + wide + 1 }
    FNR in to { printf "{%d..%d}", start, position }
  ' - "$1"
}

args=()
while IFS= read -r file; do
  if code_changed "$file"; then
    args+=(--mutate "**/$file$(spans "$file")")
  fi
done <<<"$changed"

if [ "${#args[@]}" -eq 0 ]; then
  echo "Only comments or whitespace changed in C# files since $SINCE, so there is nothing to mutate."
  exit 0
fi

dotnet tool restore
dotnet stryker "${args[@]}"
