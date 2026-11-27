#!/usr/bin/env bash
# Tests for tools/ci/static-check.sh (#273): actionlint on the workflows, shellcheck on tools/ and deploy/ scripts.
# Fixture repos (actionlint needs a git root): a clean one passes; an expression typo, an unquoted variable and a plain-scalar "run: x: y" fail.
# Needs docker. Usage: tools/ci/static-check.test.sh   (exit 0 = all pass)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

root=$(git -C "$(dirname "$0")" rev-parse --show-toplevel) || exit 2
check="$root/tools/ci/static-check.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# fixture <name>: a clean mini repo in $work/<name>
fixture() {
  git init -q "$work/$1"
  mkdir -p "$work/$1/.github/workflows" "$work/$1/tools/ci" "$work/$1/deploy"
  cat > "$work/$1/.github/workflows/ci.yml" <<'Y'
name: CI
on: push
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - run: echo "${{ github.sha }}"
Y
  printf '#!/usr/bin/env bash\nset -u\nname=$1\necho "$name"\n' > "$work/$1/tools/ci/ok.sh"
}

# expect <case> <want exit> <want output pattern> <fixture>
expect() {
  local out code
  out=$("$check" "$work/$4" 2>&1)
  code=$?
  if [ "$code" -eq "$2" ] && grep -qE -- "$3" <<<"$out"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1 (want exit $2 and output matching: $3; got exit $code)"
    echo "$out" | sed 's/^/      /'
    failures=$((failures + 1))
  fi
}

fixture clean
expect "a clean workflow and script pass" 0 "static-check: ok" clean

fixture typo
sed 's/github.sha/github.shaa/' "$work/typo/.github/workflows/ci.yml" > "$work/typo/ci.tmp" && mv "$work/typo/ci.tmp" "$work/typo/.github/workflows/ci.yml"
expect "an expression typo in a workflow fails" 1 "github.shaa|property" typo

fixture scalar
printf '      - run: echo hint: this is a plain scalar\n' >> "$work/scalar/.github/workflows/ci.yml"
expect "a run: value with a colon-space (invalid YAML) fails" 1 "ci.yml" scalar

fixture unquoted
printf '#!/usr/bin/env bash\nrm -rf $1/build\n' > "$work/unquoted/deploy/bad.sh"
expect "an unquoted variable in a deploy script fails" 1 "SC2086" unquoted

fixture unquoted-tools
printf '#!/usr/bin/env bash\nrm -rf $1/build\n' > "$work/unquoted-tools/tools/ci/bad.sh"
expect "an unquoted variable in a tools script fails" 1 "SC2086" unquoted-tools

fixture reasoned
printf '#!/usr/bin/env bash\n# word splitting is wanted here\n# shellcheck disable=SC2086\nrm -rf $1/build\n' > "$work/reasoned/deploy/ok.sh"
expect "a script with a single disable passes" 0 "static-check: ok" reasoned

if [ "$failures" -gt 0 ]; then
  echo "$failures failure(s)"
  exit 1
fi
echo "all checks passed"
