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

# 5. The rebuild claim is about base images only (NuGet still restores on a rebuild, #357).
if grep -qiE 'rebuild downloads nothing' "$README"; then
  fail "README.md says a rebuild downloads nothing; it should say a rebuild downloads no base images"
else
  ok "README.md scopes the rebuild claim to base images"
fi

# 6. Step (a) says it is not usable on develop until #340 lands (the bullet for (a) only).
if tr '\n' ' ' <"$README" | grep -qE '\(a\)[^;]*#340[^;]*until'; then
  ok "README.md says step (a) waits for #340"
else
  fail "README.md doesn't say step (a) is unavailable until #340 lands"
fi

# 7. The script is executable in git (mode 100755), like the other deploy/compose checks, so its usage line works.
#    Checked in the index, not the worktree: a Windows-mounted checkout reports every file as executable.
if git -C "$ROOT" ls-files -s -- deploy/compose/local-build.test.sh | grep -q '^100755 '; then
  ok "local-build.test.sh is executable"
else
  fail "local-build.test.sh is not executable (run: git update-index --chmod=+x deploy/compose/local-build.test.sh)"
fi

if [ "$failures" -gt 0 ]; then
  printf '%s check(s) failed\n' "$failures" >&2
  exit 1
fi
echo "all local-build checks pass"
