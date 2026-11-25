#!/usr/bin/env bash
# Acceptance check for #358: a local build uses the base images already on disk, and the README says the order.
#
# Run it from anywhere in the repo:  deploy/compose/local-build.test.sh
# Needs nothing but the repo. It reads services.yml and README.md; it builds nothing.
# The runtime evidence (a second build reaches no registry when the base images are local) is the probe in
# README.md, "Local builds"; it was run by hand on 2026-11-25 (see the PR's Tests section).
#
# Checks:
#   1. no service build forces a registry pull: no `--pull`, no `pull: true`, no `pull_policy`
#   2. README.md states the order (a) → (b) → (c) for a local run
#   3. README.md says a first build with no base images on disk is slow because of MCR
#   4. README.md gives the probe command that shows a second build pulls nothing
set -euo pipefail

ROOT=$(git rev-parse --show-toplevel)
COMPOSE_DIR=$ROOT/deploy/compose
README=$COMPOSE_DIR/README.md
SERVICES=$COMPOSE_DIR/services.yml

failures=0
fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$*" >&2
}
ok() { printf 'ok: %s\n' "$*"; }

# 1. No forced pull in the compose files or the README's build commands.
if grep -nE -- '--pull|pull:[[:space:]]*true|pull_policy' "$SERVICES" "$COMPOSE_DIR/compose.yaml" "$COMPOSE_DIR/infrastructure.yml" "$COMPOSE_DIR/monitoring.yml" >/dev/null; then
  fail "a compose file forces a pull (--pull, pull: true or pull_policy)"
  grep -nE -- '--pull|pull:[[:space:]]*true|pull_policy' "$SERVICES" "$COMPOSE_DIR/compose.yaml" "$COMPOSE_DIR/infrastructure.yml" "$COMPOSE_DIR/monitoring.yml" | sed 's/^/      /' >&2
else
  ok "no compose file forces a registry pull"
fi
if grep -nE '^[[:space:]]*docker .*--pull' "$README" >/dev/null; then
  fail "README.md documents a docker command with --pull"
else
  ok "README.md documents no build with --pull"
fi

# 2. The order, cheapest source first.
if tr '\n' ' ' <"$README" | grep -qE '\(a\).*\(b\).*\(c\)'; then
  ok "README.md states the order (a) → (b) → (c)"
else
  fail "README.md doesn't state the order (a) → (b) → (c) for a local run"
fi

# 3. A slow first build is MCR, not the build.
if grep -qiE 'first build.*slow.*(MCR|mcr\.microsoft\.com)' "$README"; then
  ok "README.md says a first build is slow because of MCR"
else
  fail "README.md doesn't say a first build with no base images on disk is slow because of MCR"
fi

# 4. The probe command is documented.
if grep -qF 'Local builds' "$README" && grep -qE 'docker compose build' "$README" && grep -qE 'pulled nothing|pulls nothing|no pull' "$README"; then
  ok "README.md documents the probe that a second build pulls nothing"
else
  fail "README.md doesn't document the probe that a second build pulls nothing"
fi

if [ "$failures" -gt 0 ]; then
  printf '%s check(s) failed\n' "$failures" >&2
  exit 1
fi
echo "all local-build checks pass"
