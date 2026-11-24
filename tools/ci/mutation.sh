#!/usr/bin/env bash
# Mutation testing on the C# files a PR changes (issue #53, docs/CSHARP_SETUP_TODO.md section 10 job 6).
# Runs the dotnet-stryker pinned in dotnet-tools.json. stryker-config.json (code-owned) holds the solution,
# the reporters and the break threshold: a mutation score below it exits non-zero.
#
# The changed files go to --mutate, not to Stryker's own --since: in solution mode Stryker 5.1 counts every
# changed file as a test file and filters out the mutants of a changed source file.
#
# Usage: SINCE=<git ref to compare with> tools/ci/mutation.sh   (in CI: SINCE=origin/<base branch>)
set -euo pipefail

: "${SINCE:?SINCE must be the git ref to compare with, e.g. origin/develop}"

changed=$(git diff --name-only --diff-filter=d "$SINCE"...HEAD -- '*.cs')
if [ -z "$changed" ]; then
  echo "No C# files changed since $SINCE, so there is nothing to mutate."
  exit 0
fi

args=()
while IFS= read -r file; do
  args+=(--mutate "**/$file")
done <<<"$changed"

dotnet tool restore
dotnet stryker "${args[@]}"
