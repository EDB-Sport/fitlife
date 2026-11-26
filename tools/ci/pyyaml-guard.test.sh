#!/usr/bin/env bash
# The PyYAML guard of the tests that read YAML (#449): without PyYAML they skip on a laptop, but fail in CI (CI=true, set by
# GitHub Actions), so a missing PyYAML can't turn ci-scripts green while the tests test nothing.
# A python3 shim on PATH plays a python3 without PyYAML, as in #330.
# Usage: tools/ci/pyyaml-guard.test.sh   (from anywhere in the repo; exit 0 = all pass)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

root=$(git -C "$(dirname "$0")" rev-parse --show-toplevel) || exit 2
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
mkdir -p "$work/bin"
printf '#!/usr/bin/env bash\nexit 1\n' > "$work/bin/python3"
chmod +x "$work/bin/python3"

failures=0
# expect <case> <want exit> <want output pattern> <CI value or "unset"> <script>
expect() {
  local out code
  if [ "$4" = unset ]; then
    out=$(env -u CI PATH="$work/bin:$PATH" "$root/$5" 2>&1)
  else
    out=$(CI="$4" PATH="$work/bin:$PATH" "$root/$5" 2>&1)
  fi
  code=$?
  if [ "$code" -eq "$2" ] && grep -qE -- "$3" <<<"$out"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1 (want exit $2 and output matching: $3; got exit $code)"
    echo "$out" | sed 's/^/      /'
    failures=$((failures + 1))
  fi
}

for script in tools/ci/dependabot.test.sh tools/ci/release-single-build.test.sh; do
  expect "$script without PyYAML in CI: fails and says how to install it" 1 '^FAIL .*PyYAML.*pip install pyyaml' true "$script"
  expect "$script without PyYAML on a laptop: skips" 0 '^SKIP: PyYAML not installed' unset "$script"
done

[ "$failures" -eq 0 ] || exit 1
