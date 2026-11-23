#!/usr/bin/env bash
# What a merge to main releases (issue #95, docs/CSHARP_SETUP_TODO.md section 9, METHODOLOGY section 2.1).
# Every service whose .csproj version has no <prefix>/vX.Y.Z tag yet is released: a git tag and the Docker Hub
# images X.Y.Z, X.Y, X and latest. A service whose version is already tagged is skipped, so a re-run is safe.
# A release/x.y.z merge also gets the tag vx.y.z for the release as a whole; a hotfix gets only its service tag.
# It only plans: it prints the tags and images (the dry run), and in GitHub Actions writes them to $GITHUB_OUTPUT
# for release.yml, which builds, scans, pushes and tags.
# Usage: RELEASE_REF=release/1.2.0 DOCKERHUB_NAMESPACE=<namespace> tools/ci/release-plan.sh   (from the repo root)
set -uo pipefail

release_ref=${RELEASE_REF:-}
namespace=${DOCKERHUB_NAMESPACE:-<namespace>}

# shellcheck source=tools/ci/services.sh
. "$(dirname "$0")/services.sh"

tag_exists() { git rev-parse -q --verify "refs/tags/$1" >/dev/null; }

matrix=()
tags=()
failed=0
while read -r name prefix version; do
  tag="$prefix/v$version"
  if tag_exists "$tag"; then
    echo "$name: $tag exists, skipped"
    continue
  fi
  if [ ! -f "src/$name/Dockerfile" ]; then
    echo "::error title=Release::$name $version has no src/$name/Dockerfile, so it can't be released."
    failed=1
    continue
  fi
  major=${version%%.*}
  minor=${version#*.}; minor=${minor%%.*}
  echo "$name: tag $tag"
  echo "$name: image $namespace/$prefix: $version $major.$minor $major latest"
  tags+=("$tag")
  matrix+=("{\"name\":\"$name\",\"prefix\":\"$prefix\",\"version\":\"$version\"}")
done < <(services)

if [[ $release_ref =~ ^release/([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
  release_tag="v${BASH_REMATCH[1]}"
  if tag_exists "$release_tag"; then
    echo "release: $release_tag exists, skipped"
  elif [ "${#tags[@]}" -gt 0 ]; then
    echo "release: tag $release_tag"
    tags+=("$release_tag")
  fi
fi

if [ "${#tags[@]}" -eq 0 ] && [ "$failed" -eq 0 ]; then
  echo "Nothing to release: every service's version is already tagged."
fi

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  services_json=$(IFS=,; echo "[${matrix[*]}]")
  echo "services=$services_json" >> "$GITHUB_OUTPUT"
  echo "tags=${tags[*]}" >> "$GITHUB_OUTPUT"
fi
exit "$failed"
