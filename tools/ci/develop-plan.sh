#!/usr/bin/env bash
# What a push to develop delivers (issue #340, docs/TECHNICAL_REFERENCE.md section 9, METHODOLOGY section 2.1).
# Every service whose :develop image on Docker Hub is out of date gets the images <namespace>/<prefix>:develop and
# :sha-<short>. Out of date means: the image is missing, or its source changed since the commit the image was built
# from (its revision label, tools/ci/delivered-revision.sh). "Changed" is the rule of release-gate.sh
# (tools/ci/changes.sh): src/<Service>/, a libs/ project it references, a libs/ file it links in, or a root build file.
# It compares with what was delivered, not with the previous push, so a push whose run was cancelled (GitHub keeps one
# pending run per concurrency group) or failed is made up by the next run, and the first run after this workflow
# lands delivers every service. Never latest and never an X.Y.Z tag: those are the release's (release-plan.sh).
# It only plans: it prints the images (the dry run), and in GitHub Actions writes them to $GITHUB_OUTPUT for
# develop-images.yml, which builds, scans and pushes.
# Usage: DOCKERHUB_NAMESPACE=<namespace> tools/ci/develop-plan.sh   (from the repo root)
# DELIVERED_REVISION: the command that prints an image's revision (default tools/ci/delivered-revision.sh; tests stub it)
set -uo pipefail

lookup=${DELIVERED_REVISION:-$(dirname "$0")/delivered-revision.sh}
namespace=${DOCKERHUB_NAMESPACE:-<namespace>}
short=$(git rev-parse --short=7 HEAD)

# shellcheck source=tools/ci/services.sh
. "$(dirname "$0")/services.sh"
# shellcheck source=tools/ci/changes.sh
. "$(dirname "$0")/changes.sh"

matrix=()
failed=0
while read -r name prefix _; do
  before=$("$lookup" "$namespace/$prefix")
  if [[ $before =~ ^[0-9a-f]{40}$ ]] && git cat-file -e "$before^{commit}" 2>/dev/null; then
    shared=$(shared_changes "$before" "src/$name/$name.csproj")
    if git diff --quiet "$before" HEAD -- "src/$name/" && [ -z "$shared" ]; then
      echo "$name: unchanged since its :develop image"
      continue
    fi
  else
    echo "$name: no :develop image built from a commit of this repo, so it is planned"
  fi
  if [ ! -f "src/$name/Dockerfile" ]; then
    echo "::error title=Develop images::$name has no src/$name/Dockerfile, so its image can't be delivered."
    failed=1
    continue
  fi
  echo "$name: image $namespace/$prefix: develop sha-$short"
  matrix+=("{\"name\":\"$name\",\"prefix\":\"$prefix\"}")
done < <(services)

if [ "${#matrix[@]}" -eq 0 ] && [ "$failed" -eq 0 ]; then
  echo "Nothing to deliver: every :develop image is current."
fi

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  # ${arr[*]+...}: an empty array is unbound under set -u in bash 3.2 (macOS, #87)
  echo "services=$(IFS=,; echo "[${matrix[*]+${matrix[*]}}]")" >> "$GITHUB_OUTPUT"
fi
exit "$failed"
