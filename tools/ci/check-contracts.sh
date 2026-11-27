#!/usr/bin/env bash
# Fails when a committed contract is stale (issues #90 and #491, docs/TECHNICAL_REFERENCE.md section 8).
# The build regenerates contracts/openapi/<service>.json and, through tools/EventSchemaGenerator,
# contracts/events/<routing key>.schema.json; any difference from the commit means an API or an
# integration event record changed without its contract. Usage: tools/ci/check-contracts.sh   (from the repo root)
set -uo pipefail

if ! dotnet build -c Release -v q -clp:NoSummary -tl:off; then
  echo "::error title=Contract::The build failed, so the contracts could not be generated."
  exit 1
fi

# Compared with the index, which is the commit in a CI checkout. New services' contracts show as untracked files.
untracked=$(git ls-files --others --exclude-standard -- contracts/openapi/ contracts/events/)
if ! git diff --quiet -- contracts/openapi/ contracts/events/ || [ -n "$untracked" ]; then
  echo "::error title=Contract::The committed contracts are stale. Build locally and commit contracts/openapi/ and contracts/events/:"
  git --no-pager diff -- contracts/openapi/ contracts/events/
  [ -z "$untracked" ] || echo "Not committed: $untracked"
  exit 1
fi
echo "The contracts in contracts/openapi/ and contracts/events/ are up to date."
