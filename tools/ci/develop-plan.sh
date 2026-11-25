#!/usr/bin/env bash
# What a push to develop delivers (issue #340, docs/CSHARP_SETUP_TODO.md section 9, METHODOLOGY section 2.1).
# Every service that changed in the push (BEFORE..HEAD) gets the Docker Hub images <namespace>/<prefix>:develop and
# :sha-<short>. "Changed" is the rule of release-gate.sh (tools/ci/changes.sh): src/<Service>/, a libs/ project it
# references, a libs/ file it links in, or a root build file. Never latest and never an X.Y.Z tag: those are the
# release's (release-plan.sh). No earlier commit (the first push, or one that isn't in this clone) plans every service.
# It only plans: it prints the images (the dry run), and in GitHub Actions writes them to $GITHUB_OUTPUT for
# develop-images.yml, which builds, scans and pushes.
# Usage: BEFORE=<sha before the push> DOCKERHUB_NAMESPACE=<namespace> tools/ci/develop-plan.sh   (from the repo root)
set -uo pipefail

before=${BEFORE:-}
namespace=${DOCKERHUB_NAMESPACE:-<namespace>}
short=$(git rev-parse --short=7 HEAD)

# shellcheck source=tools/ci/services.sh
. "$(dirname "$0")/services.sh"
# shellcheck source=tools/ci/changes.sh
. "$(dirname "$0")/changes.sh"

all=0
if [[ ! $before =~ ^[0-9a-f]{40}$ ]] || [[ $before =~ ^0+$ ]] || ! git cat-file -e "$before^{commit}" 2>/dev/null; then
  all=1
  echo "No earlier commit to compare with: every service is planned."
fi

matrix=()
failed=0
while read -r name prefix _; do
  if [ "$all" -eq 0 ]; then
    shared=$(shared_changes "$before" "src/$name/$name.csproj")
    if git diff --quiet "$before" HEAD -- "src/$name/" && [ -z "$shared" ]; then
      echo "$name: unchanged"
      continue
    fi
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
  echo "Nothing to deliver: no service changed."
fi

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  # ${arr[*]+...}: an empty array is unbound under set -u in bash 3.2 (macOS, #87)
  echo "services=$(IFS=,; echo "[${matrix[*]+${matrix[*]}}]")" >> "$GITHUB_OUTPUT"
fi
exit "$failed"
