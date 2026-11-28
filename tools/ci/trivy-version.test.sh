#!/usr/bin/env bash
# Every Trivy step names the Trivy version it runs, and the PR scan and the release scan run the same one (issue #526).
# trivy-action's own default is old (v0.70.0 in v0.36.0), and Dependabot bumps the action's SHA, not its version:
# input, so the version is set here and bumped by hand.
# Usage: tools/ci/trivy-version.test.sh   (exit 0 = every check passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0
want=v0.76.2

# trivy_versions <workflow file>: the version: of each trivy-action step, one per line ("none" when it has none)
trivy_versions() {
  awk '
    function flush() { if (inside) print (version == "" ? "none" : version); inside = 0; version = "" }
    /uses: aquasecurity\/trivy-action@/ { flush(); inside = 1; match($0, /^ */); indent = RLENGTH; next }
    inside {
      match($0, /^ */)
      if ($0 ~ /^ *- / && RLENGTH <= indent - 2) { flush(); next }
      if ($0 ~ /^ *version: /) { version = $0; sub(/^ *version: */, "", version); sub(/ +#.*$/, "", version); gsub(/"/, "", version) }
    }
    END { flush() }
  ' "$1"
}

# expect <description> <command...>
expect() {
  local description=$1
  shift
  if "$@"; then
    echo "PASS  $description"
  else
    echo "FAIL  $description"
    failures=$((failures + 1))
  fi
}

for name in image-scan.yml release.yml; do
  versions=$(trivy_versions "$root/.github/workflows/$name")
  expect "$name has a Trivy step" test -n "$versions"
  while IFS= read -r version; do
    [ -n "$version" ] || continue
    expect "$name runs Trivy $want (got $version)" test "$version" = "$want"
  done <<<"$versions"
done

exit $((failures > 0))
