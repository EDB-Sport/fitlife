#!/usr/bin/env bash
# Tests for the PR image scan (#280): tools/ci/image-scan-plan.sh, which picks the services whose image a PR
# changes, and .github/workflows/image-scan.yml, which builds each picked image once and scans it with Trivy.
# The plan cases run on a throwaway repo root with two stub Dockerfiles, so no Docker is needed.
# Usage: tools/ci/image-scan.test.sh   (exit code 0 = every case passes)
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)
plan="$here/image-scan-plan.sh"
workflow="$root/.github/workflows/image-scan.yml"
release="$root/.github/workflows/release.yml"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# A repo root with two services. Alpha copies the shared files and libs/, Beta only its own folder.
mkdir -p "$work/src/Alpha" "$work/src/Beta" "$work/src/NoImage"
cat > "$work/src/Alpha/Dockerfile" <<'EOF'
FROM sdk AS build
COPY global.json Directory.Build.props ./
COPY libs/ libs/
COPY src/Alpha/ src/Alpha/
FROM runtime
COPY --from=build /app/publish .
EOF
cat > "$work/src/Beta/Dockerfile" <<'EOF'
FROM sdk AS build
copy src/Beta/ src/Beta/
FROM runtime
COPY --from=build /app/publish .
EOF

# picks <changed paths...>: prints what the plan script prints for those changed paths
picks() { printf '%s\n' "$@" | IMAGE_SCAN_ROOT="$work" "$plan" 2>&1; }

# check <description> <condition...>
check() {
  local description=$1
  shift
  if "$@"; then
    echo "PASS  $description"
  else
    echo "FAIL  $description"
    failures=$((failures + 1))
  fi
}
same() { [ "$1" = "$2" ] || { echo "      got: $1, want: $2"; false; }; }

# 1. Which services a change picks
check "a change outside every image picks none" same "$(picks docs/README.md tests/Alpha.Tests/A.cs)" '[]'
check "nothing changed picks none" same "$(printf '' | IMAGE_SCAN_ROOT="$work" "$plan" 2>&1)" '[]'
check "a file in the service's folder picks that service" same "$(picks src/Alpha/Program.cs)" '["Alpha"]'
check "the service's Dockerfile picks that service" same "$(picks src/Beta/Dockerfile)" '["Beta"]'
check "a lower-case copy line counts too" same "$(picks src/Beta/Program.cs)" '["Beta"]'
check "a copied shared folder picks every service that copies it" same "$(picks libs/Logging/nlog.config)" '["Alpha"]'
check "a copied root file picks every service that copies it" same "$(picks global.json)" '["Alpha"]'
check "a path that only starts like a copied folder picks none" same "$(picks libsextra/x.cs src/AlphaTools/x.cs)" '[]'
check "COPY --from copies from a stage, not the repo" same "$(picks app/publish/x.dll)" '[]'
check "a folder without a Dockerfile picks none" same "$(picks src/NoImage/x.cs)" '[]'
check ".dockerignore picks every service" same "$(picks .dockerignore)" '["Alpha","Beta"]'
for scan_file in .github/workflows/image-scan.yml tools/ci/image-scan-plan.sh tools/ci/check-service-image.sh; do
  check "a change to the scan itself ($scan_file) picks every service" same "$(picks "$scan_file")" '["Alpha","Beta"]'
done
check "each service is picked once" same "$(picks src/Alpha/a.cs src/Alpha/b.cs libs/x)" '["Alpha"]'
# macOS /bin/bash is 3.2, which has no mapfile or readarray (#87)
check "the plan runs on bash 3.2 (no mapfile or readarray)" \
  bash -c '! grep -vE "^\s*#" "$1" | grep -qwE "mapfile|readarray"' _ "$plan"

# 2. The real repo: every Dockerfile under src/ is found
real=$(printf '.dockerignore\n' | "$plan")
for dockerfile in "$root"/src/*/Dockerfile; do
  service=$(basename "$(dirname "$dockerfile")")
  check "the real plan knows $service" grep -qF "\"$service\"" <<<"$real"
done

# 3. The workflow
if [ ! -f "$workflow" ]; then
  echo "FAIL  .github/workflows/image-scan.yml exists"
  failures=$((failures + 1))
else
  # on_block: the lines of the top-level "on:" key
  on_block=$(awk '/^on:/ { inside = 1; next } inside && /^[A-Za-z_-]+:/ { exit } inside { print }' "$workflow")
  check "the workflow runs on pull requests" grep -qE '^  pull_request:' <<<"$on_block"
  check "the workflow has no paths filter (the plan job decides, so a skip still reports)" \
    bash -c '! grep -qE "^\s+paths(-ignore)?:" <<<"$1"' _ "$on_block"
  check "the plan job runs tools/ci/image-scan-plan.sh" grep -qF 'tools/ci/image-scan-plan.sh' "$workflow"
  check "the scan job is skipped when the plan picks no service" \
    grep -qF "if: needs.image-scan-plan.outputs.services != '[]'" "$workflow"
  steps=$(grep -vE '^\s*#' "$workflow") # the workflow without its comment lines
  check "the image is built once, by check-service-image.sh --build-only" \
    test "$(grep -c 'docker build\|check-service-image.sh' <<<"$steps")" -eq 1 -a \
    "$(grep -c 'tools/ci/check-service-image.sh --build-only' <<<"$steps")" -eq 1
  check "Trivy scans the image that build step tagged" grep -qF 'image-ref: ${{ steps.build.outputs.image }}' "$workflow"
  # trivy_with <file>: the uses: line of the Trivy step and its with: settings
  trivy_with() { grep -A5 'uses: aquasecurity/trivy-action@' "$1" | grep -E 'uses:|severity:|ignore-unfixed:|exit-code:' | sed 's/^ *//'; }
  check "Trivy is pinned and set like release.yml (severity, ignore-unfixed, exit-code)" \
    same "$(trivy_with "$workflow")" "$(trivy_with "$release")"
  check "the Trivy action is pinned to a commit SHA" grep -qE 'uses: aquasecurity/trivy-action@[0-9a-f]{40} # v' "$workflow"
fi

echo
if [ "$failures" -eq 0 ]; then
  echo "All image-scan cases pass."
else
  echo "$failures case(s) failed."
fi
exit "$failures"
