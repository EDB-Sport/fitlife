#!/usr/bin/env bash
# Acceptance check for #514: the deployment diagram draws every compose service as a running container.
#
# Run it from anywhere in the repo:  deploy/compose/deployment-diagram.test.sh
# Needs nothing but the repo. It reads deploy/compose/*.yml and docs/architecture/deployment.md; it starts nothing.
# It checks the boxes, not the arrows between them.
#
# Checks, for every service in deploy/compose/*.yml:
#   1. the diagram has a box labelled with its compose name
#   2. that box isn't marked <<planned>>
set -euo pipefail

ROOT=$(git rev-parse --show-toplevel)
DOC=$ROOT/docs/architecture/deployment.md

failures=0
fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$*" >&2
}
ok() { printf 'ok: %s\n' "$*"; }

# The keys two spaces in under the top-level "services:" key of each file; the next top-level key ends the block.
services=$(awk '/^[^[:space:]#]/ { in_services = /^services:/ }
  in_services && /^  [A-Za-z0-9_-]+:/ { sub(/:.*/, ""); print $1 }' "$ROOT"/deploy/compose/*.yml)
[[ -n $services ]] || fail "found no services in deploy/compose/*.yml"

for service in $services; do
  # A box is a line such as: rectangle "booking-svc\nBookingService" as booking
  box=$(grep -E "^[[:space:]]*(rectangle|database|queue) \"$service(\\\\n|\")" "$DOC" || true)
  if [[ -z $box ]]; then
    fail "deployment.md has no box for $service"
  elif [[ $box == *'<<planned>>'* ]]; then
    fail "deployment.md marks $service as <<planned>>, but it is in the compose files"
  else
    ok "deployment.md draws $service as a running container"
  fi
done

if [[ $failures -gt 0 ]]; then
  printf '%s check(s) failed\n' "$failures" >&2
  exit 1
fi
echo "all deployment-diagram checks pass"
