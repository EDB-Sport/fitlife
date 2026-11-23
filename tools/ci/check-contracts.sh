#!/usr/bin/env bash
# Fails when a committed OpenAPI contract is stale (issue #90, docs/CSHARP_SETUP_TODO.md section 8).
# The build regenerates contracts/openapi/<service>.json; any difference from the commit means
# the API changed without its contract. Usage: tools/ci/check-contracts.sh   (from the repo root)
set -uo pipefail

if ! dotnet build -c Release -v q -clp:NoSummary -tl:off; then
  echo "::error title=OpenAPI contract::The build failed, so the contracts could not be generated."
  exit 1
fi

changes=$(git status --porcelain -- contracts/openapi/)
if [ -n "$changes" ]; then
  echo "::error title=OpenAPI contract::The committed OpenAPI contracts are stale. Build locally and commit contracts/openapi/:"
  echo "$changes"
  git --no-pager diff -- contracts/openapi/
  exit 1
fi
echo "The OpenAPI contracts in contracts/openapi/ are up to date."
