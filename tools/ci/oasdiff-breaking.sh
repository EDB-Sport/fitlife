#!/usr/bin/env bash
# Fails when a service's OpenAPI contract breaks its contract at the service's last release tag (issue #91,
# docs/CSHARP_SETUP_TODO.md section 8). Compares contracts/openapi/<service>.json in the working tree with the
# same file at the newest <service>/vX.Y.Z tag. A service with no tag yet (before its first release) is skipped.
# Usage: tools/ci/oasdiff-breaking.sh [<service> ...]   (from the repo root; no arguments = every contract)
# Exit code 0 = no breaking change, 1 = at least one. The release gate (#94) reuses it per service.
# oasdiff runs from its pinned image, so only git and Docker are needed.
set -uo pipefail

image="tufin/oasdiff:v1.37.0"

if [ "$#" -gt 0 ]; then
  services=("$@")
else
  services=()
  for file in contracts/openapi/*.json; do
    [ -e "$file" ] && services+=("$(basename "$file" .json)")
  done
fi

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
broken=0

for service in ${services[@]+"${services[@]}"}; do # guarded: an empty array is unbound under set -u in bash 3.2 (#87)
  tag=$(git tag --list "$service/v*" --sort=-v:refname | head -1)
  if [ -z "$tag" ]; then
    echo "$service: no $service/v* tag yet, skipped"
    continue
  fi
  if ! git show "$tag:contracts/openapi/$service.json" > "$work/base.json" 2>/dev/null; then
    echo "$service: $tag has no contracts/openapi/$service.json, skipped"
    continue
  fi
  cp "contracts/openapi/$service.json" "$work/revision.json"

  echo "$service: comparing with $tag"
  if ! docker run --rm -v "$work:/specs:ro" "$image" breaking /specs/base.json /specs/revision.json --fail-on ERR; then
    echo "::error title=Breaking API change::$service breaks its contract at $tag. Keep the old interface (add /v2/ next to /v1/, or emulate it), or release $service as a new MAJOR."
    broken=1
  fi
done

exit "$broken"
