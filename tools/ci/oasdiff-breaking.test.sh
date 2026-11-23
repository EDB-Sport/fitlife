#!/usr/bin/env bash
# Tests for tools/ci/oasdiff-breaking.sh (issue #91), in a throwaway git repo:
# a removed endpoint fails, an added endpoint passes, a service with no tag is skipped.
# Needs git and Docker (oasdiff runs from its pinned image). Usage: tools/ci/oasdiff-breaking.test.sh
set -uo pipefail

script="$(cd "$(dirname "$0")" && pwd)/oasdiff-breaking.sh"
repo=$(mktemp -d)
trap 'rm -rf "$repo"' EXIT
failures=0

# contract <paths JSON>: an OpenAPI 3.1 document with these paths
contract() {
  printf '{"openapi":"3.1.1","info":{"title":"demo","version":"1.0.0"},"paths":%s}\n' "$1"
}
get_op='{"get":{"responses":{"200":{"description":"OK"}}}}'

git -C "$repo" init -q
git -C "$repo" config user.email test@example.invalid
git -C "$repo" config user.name test
mkdir -p "$repo/contracts/openapi"
contract "{\"/members\":$get_op,\"/classes\":$get_op}" > "$repo/contracts/openapi/demo.json"
git -C "$repo" add -A && git -C "$repo" commit -qm "demo 1.0.0" && git -C "$repo" tag demo/v1.0.0

# expect <0|1> <description> [grep pattern for the output]
expect() {
  local out status
  out=$(cd "$repo" && "$script" 2>&1)
  status=$?
  if [ "$status" -eq "$1" ] && { [ -z "${3:-}" ] || grep -q "$3" <<<"$out"; }; then
    echo "PASS  $2"
  else
    echo "FAIL  $2 (exit $status, expected $1)"
    echo "$out" | tail -10
    failures=$((failures + 1))
  fi
}

expect 0 "an unchanged contract passes"

contract "{\"/members\":$get_op}" > "$repo/contracts/openapi/demo.json"
expect 1 "a removed endpoint fails" "api-path-removed"

contract "{\"/members\":$get_op,\"/classes\":$get_op,\"/waitlist\":$get_op}" > "$repo/contracts/openapi/demo.json"
expect 0 "an added endpoint passes"

contract "{\"/members\":$get_op}" > "$repo/contracts/openapi/fresh.json"
expect 0 "a service with no tag is skipped" "fresh: no fresh/v\* tag yet, skipped"

echo
if [ "$failures" -eq 0 ]; then echo "oasdiff-breaking.sh works."; else echo "$failures check(s) failed."; fi
exit "$failures"
