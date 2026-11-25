#!/usr/bin/env bash
# Every `uses:` in .github/ names a full 40-character commit SHA, with the release tag in a comment (issue #149),
# so "Require actions to be pinned to a full-length commit SHA" can be turned on without breaking a workflow.
# Local actions (./...) and docker:// images are not GitHub actions and are skipped.
# Usage: tools/ci/actions-pinned.test.sh   (from the repo root, or anywhere in the repo)
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2
failures=0

while IFS=: read -r file line text; do
  ref=$(sed -E 's/^[[:space:]-]*uses:[[:space:]]*//; s/[[:space:]]+#.*$//; s/["'\'']//g' <<<"$text")
  case $ref in ./* | docker://*) continue ;; esac
  if ! grep -Eq '^[^@[:space:]]+@[0-9a-f]{40}$' <<<"$ref"; then
    echo "FAIL  $file:$line uses $ref: pin it to a full commit SHA, with the tag in a comment (# vX.Y.Z)"
    failures=$((failures + 1))
  elif ! grep -Eq '#[[:space:]]*v?[0-9]' <<<"$text"; then
    echo "FAIL  $file:$line uses $ref without the tag in a comment (# vX.Y.Z)"
    failures=$((failures + 1))
  fi
done < <(grep -rnE '^[[:space:]-]*uses:' .github --include='*.yml' --include='*.yaml')

if [ "$failures" -eq 0 ]; then echo "Every action is pinned to a commit SHA."; else echo "$failures action(s) not pinned."; fi
exit $((failures > 0))
