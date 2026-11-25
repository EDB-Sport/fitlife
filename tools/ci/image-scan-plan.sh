#!/usr/bin/env bash
# Plan of the PR image scan (#280, .github/workflows/image-scan.yml): which service images a PR changes.
# Reads the changed paths, one per line, on stdin. Prints a JSON array of the services under src/ whose
# Dockerfile copies a changed path (a COPY source from the repo, not COPY --from a stage), or whose
# Dockerfile itself changed. A change to .dockerignore or to the scan itself picks every service.
# Usage: git diff --name-only "$BASE_SHA...$HEAD_SHA" | tools/ci/image-scan-plan.sh
#        IMAGE_SCAN_ROOT=<dir> reads the Dockerfiles under <dir>/src/ instead of this repo's (for the tests).
set -euo pipefail

root=${IMAGE_SCAN_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}
mapfile -t changed
every_service=(.dockerignore .github/workflows/image-scan.yml tools/ci/image-scan-plan.sh tools/ci/check-service-image.sh)

# copy_sources <Dockerfile>: the repo paths its COPY lines take, one per line. Every word after COPY except
# flags and the last one (the destination). A directory keeps its trailing slash.
copy_sources() {
  awk 'toupper($1) == "COPY" {
         for (i = 2; i < NF; i++) if ($i ~ /^--from=/) next
         for (i = 2; i < NF; i++) if ($i !~ /^--/) print $i
       }' "$1"
}

# takes <input> <path>: a directory input takes every path under it, a file input only itself
takes() {
  case $1 in
    */) [[ $2 == "$1"* ]] ;;
    *) [ "$2" = "$1" ] ;;
  esac
}

picked=()
for dockerfile in "$root"/src/*/Dockerfile; do
  [ -f "$dockerfile" ] || continue
  service=$(basename "$(dirname "$dockerfile")")
  mapfile -t inputs < <(copy_sources "$dockerfile")
  inputs+=("src/$service/Dockerfile" "${every_service[@]}")
  for path in ${changed[@]+"${changed[@]}"}; do
    for input in "${inputs[@]}"; do
      if takes "$input" "$path"; then
        picked+=("$service")
        continue 3
      fi
    done
  done
done

printf '%s\n' ${picked[@]+"${picked[@]}"} | jq -Rsc 'split("\n") | map(select(. != ""))'
