#!/usr/bin/env bash
# Static checks (#273): actionlint on .github/workflows/ and shellcheck on tools/**/*.sh and deploy/**/*.sh.
# Both tools run from their official images, pinned by tag and digest as in security.yml. Change tag and digest together.
# The gate is warnings and errors (-S warning) plus the unquoted-expansion rules SC2086, SC2046, SC2068 and SC2048,
# which it rates only as info. Its other style notes (SC2016, SC2329, ...) are not gated.
# Usage: tools/ci/static-check.sh [repo root]   (default: this repo; exit 0 = clean, 1 = findings)
set -uo pipefail

ACTIONLINT_IMAGE=rhysd/actionlint:1.7.12@sha256:b1934ee5f1c509618f2508e6eb47ee0d3520686341fec936f3b79331f9315667
SHELLCHECK_IMAGE=koalaman/shellcheck:v0.11.0@sha256:61862eba1fcf09a484ebcc6feea46f1782532571a34ed51fedf90dd25f925a8d

root=${1:-$(git -C "$(dirname "$0")" rev-parse --show-toplevel)} || exit 2
cd "$root" || exit 2

status=0

echo "::group::actionlint"
if ! docker run --rm -v "$PWD:/repo:ro" -w /repo "$ACTIONLINT_IMAGE"; then
  status=1
  echo "::error title=static-check::actionlint found problems in .github/workflows/"
fi
echo "::endgroup::"

scripts=()
while IFS= read -r script; do scripts+=("$script"); done < <(find tools deploy -name '*.sh' -type f 2>/dev/null | sort)
echo "::group::shellcheck (${#scripts[@]} scripts)"
shellcheck() { docker run --rm -v "$PWD:/repo:ro" -w /repo "$SHELLCHECK_IMAGE" "$@"; }
shellcheck_status=0
if [ "${#scripts[@]}" -gt 0 ]; then
  shellcheck -S warning "${scripts[@]}" || shellcheck_status=1
  shellcheck -S style --include=SC2086,SC2046,SC2068,SC2048 "${scripts[@]}" || shellcheck_status=1
fi
if [ "$shellcheck_status" -ne 0 ]; then
  status=1
  echo "::error title=static-check::shellcheck found problems in tools/ or deploy/ scripts"
fi
echo "::endgroup::"

if [ "$status" -eq 0 ]; then
  echo "static-check: ok"
fi
exit "$status"
