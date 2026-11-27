#!/usr/bin/env bash
# diff-scan (issue #5, docs/TECHNICAL_REFERENCE.md section 10): the text-level escapes the compiler can't see.
# Looks only at what the PR changes (git diff base...head) and fails on lines it adds:
#   - ExcludeFromCodeCoverage in code or project files
#   - a new .editorconfig or .globalconfig anywhere except the repo root
#   - continue-on-error: true, or if: false, in .github/workflows/
# Labels for reviewers, printed as "label: <name>" and written to $GITHUB_OUTPUT as labels=<a,b>:
#   - tests-modified   existing lines under tests/ are removed or changed
#   - contract-change  anything under contracts/ changes
# Usage: BASE_SHA=<base commit> [HEAD_SHA=<head, default HEAD>] tools/ci/diff-scan.sh
set -uo pipefail

if [ -z "${BASE_SHA:-}" ]; then
  echo "diff-scan: BASE_SHA is not set (the PR's base commit)" >&2
  exit 2
fi
range="$BASE_SHA...${HEAD_SHA:-HEAD}"
failures=0

fail() {
  echo "FAIL  $1"
  failures=$((failures + 1))
}

# lines <+|-> [paths...]: "<file>\t<line>" for each added (+) or removed (-) line in the PR
lines() {
  local sign=$1
  shift
  git diff -U0 --no-color --no-ext-diff "$range" -- "$@" | awk -v sign="$sign" '
    /^diff --git / { hunk = 0; next }
    /^\+\+\+ /     { if (!hunk) { file = substr($0, 5); sub(/^b\//, "", file); next } }
    /^@@ /         { hunk = 1; next }
    hunk && substr($0, 1, 1) == sign { print file "\t" substr($0, 2) }
  '
}

added=$(lines +) || exit 2

while IFS=$'\t' read -r file line; do
  case "$file" in
    *.cs | *.razor | *.csproj | *.props | *.targets)
      if [[ $line == *ExcludeFromCodeCoverage* ]]; then
        fail "$file adds ExcludeFromCodeCoverage: $line"
      fi
      ;;
    .github/workflows/*)
      if [[ $line =~ continue-on-error:[[:space:]]*(true|\$\{\{[[:space:]]*true[[:space:]]*\}\}) ]]; then
        fail "$file adds continue-on-error: true: $line"
      fi
      if [[ $line =~ if:[[:space:]]*(false|\$\{\{[[:space:]]*false[[:space:]]*\}\})[[:space:]]*(#.*)?$ ]]; then
        fail "$file adds if: false: $line"
      fi
      ;;
  esac
done <<<"$added"

while read -r file; do
  case "$file" in
    */.editorconfig | */*.globalconfig) fail "$file: a new .editorconfig or .globalconfig is only allowed at the repo root" ;;
  esac
done < <(git diff --name-only --diff-filter=AR --no-color "$range")

labels=()
if [ -n "$(lines - tests/)" ]; then
  labels+=(tests-modified)
fi
if [ -n "$(git diff --name-only "$range" -- contracts/)" ]; then
  labels+=(contract-change)
fi
# ${labels[@]+...}: an empty array is "unbound" under set -u in bash 3.2 (macOS /bin/bash, #87)
for label in ${labels[@]+"${labels[@]}"}; do
  echo "label: $label"
done
if [ -n "${GITHUB_OUTPUT:-}" ]; then
  (IFS=,; echo "labels=${labels[*]+${labels[*]}}") >>"$GITHUB_OUTPUT"
fi

if [ "$failures" -gt 0 ]; then
  echo "diff-scan: $failures problem(s) in lines this PR adds"
  exit 1
fi
echo "diff-scan: no problems"
