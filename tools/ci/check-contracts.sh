#!/usr/bin/env bash
# Fails when a committed OpenAPI contract is stale (issue #90, docs/CSHARP_SETUP_TODO.md section 8).
# The build regenerates contracts/openapi/<service>.json; any difference from the commit means
# the API changed without its contract. Usage: tools/ci/check-contracts.sh   (from the repo root)
set -uo pipefail

if ! dotnet build -c Release -v q -clp:NoSummary -tl:off; then
  echo "::error title=OpenAPI contract::The build failed, so the contracts could not be generated."
  exit 1
fi

# Compared with the index, which is the commit in a CI checkout. New services' contracts show as untracked files.
untracked=$(git ls-files --others --exclude-standard -- contracts/openapi/)
if ! git diff --quiet -- contracts/openapi/ || [ -n "$untracked" ]; then
  echo "::error title=OpenAPI contract::The committed OpenAPI contracts are stale. Build locally and commit contracts/openapi/:"
  git --no-pager diff -- contracts/openapi/
  [ -z "$untracked" ] || echo "Not committed: $untracked"
  exit 1
fi
echo "The OpenAPI contracts in contracts/openapi/ are up to date."
