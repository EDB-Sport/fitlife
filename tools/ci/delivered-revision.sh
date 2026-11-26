#!/usr/bin/env bash
# The commit a service's :develop image was built from (issue #340): the org.opencontainers.image.revision label that
# docker/metadata-action sets, read from Docker Hub without pulling the image. Prints nothing when the image doesn't
# exist, can't be read or has no label: develop-plan.sh then plans the service. The repositories are public.
# Usage: tools/ci/delivered-revision.sh <namespace>/<prefix>
set -uo pipefail
docker buildx imagetools inspect "$1:develop" --format '{{json .Image}}' 2>/dev/null |
  jq -r '(.config.Labels // {})["org.opencontainers.image.revision"] // empty' 2>/dev/null
exit 0
