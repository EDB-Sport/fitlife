#!/usr/bin/env bash
# Acceptance check for #585: one PORT_OFFSET shifts every host port the compose stack publishes, so two stacks
# run side by side on one Docker host.
#
# Run it from anywhere in the repo:  deploy/compose/port-offset.test.sh
# Needs Docker with Compose v2 and jq. It only renders the configuration (docker compose config); it starts nothing.
#
# Checks:
#   1. compose.yaml publishes at least one host port (so the checks below compare something)
#   2. with ports.sh sourced and PORT_OFFSET=0, every published port is what it is without ports.sh
#   3. with PORT_OFFSET=100, every published port is 100 higher: a port ports.sh doesn't shift fails here
#   4. a *_PORT set in the shell still wins over the offset
#   5. .env.example and README.md describe PORT_OFFSET, and README.md the project name (-p)
set -euo pipefail

ROOT=$(git rev-parse --show-toplevel)
COMPOSE_DIR=$ROOT/deploy/compose
ENV_FILE=$COMPOSE_DIR/.env.example
README=$COMPOSE_DIR/README.md

failures=0
fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$*" >&2
}
ok() { printf 'ok: %s\n' "$*"; }

# published [VAR=value]... : "<service>/<container port>=<host port>" per published port, sorted, from
# `docker compose config` with only .env.example and the given variables (as tools/ci/deploy-check.sh: nothing
# from your shell or .env). With PORT_OFFSET given, ports.sh is sourced first, as the README says.
published() {
  local keep=(PATH="$PATH" HOME="$HOME")
  local var
  for var in DOCKER_HOST DOCKER_CONTEXT DOCKER_CONFIG DOCKER_CERT_PATH DOCKER_TLS_VERIFY; do
    if [[ -n ${!var:-} ]]; then keep+=("$var=${!var}"); fi
  done
  local source_ports=":"
  local arg
  for arg in "$@"; do
    if [[ $arg == PORT_OFFSET=* ]]; then source_ports='. "$COMPOSE_DIR/ports.sh"'; fi
  done
  env -i "${keep[@]}" COMPOSE_DIR="$COMPOSE_DIR" ENV_FILE="$ENV_FILE" "$@" sh -c "$source_ports"' &&
    docker compose --project-directory "$COMPOSE_DIR" --env-file "$ENV_FILE" -f "$COMPOSE_DIR/compose.yaml" config --format json' |
    jq -r '.services | to_entries[] | .key as $s | (.value.ports // [])[] | "\($s)/\(.target)=\(.published)"' | sort
}

before=$(published)
count=$(grep -c = <<<"$before" || true)

# 1. Something to compare.
if [[ $count -gt 0 ]]; then
  ok "compose.yaml publishes $count host ports"
else
  fail "compose.yaml publishes no host port: nothing to compare"
fi

# 2. Offset 0 changes nothing.
if [[ ! -f $COMPOSE_DIR/ports.sh ]]; then
  fail "deploy/compose/ports.sh doesn't exist"
else
  at_zero=$(published PORT_OFFSET=0 2>/dev/null) || true
  if [[ $at_zero == "$before" ]]; then
    ok "PORT_OFFSET=0: every published port is what it is today"
  else
    fail "PORT_OFFSET=0 changes the published ports:"$'\n'"$(diff <(echo "$before") <(echo "$at_zero") || true)"
  fi

  # 3. Offset 100 shifts every port.
  wanted=$(awk -F= '{ print $1 "=" $2 + 100 }' <<<"$before")
  at_hundred=$(published PORT_OFFSET=100 2>/dev/null) || true
  if [[ $at_hundred == "$wanted" ]]; then
    ok "PORT_OFFSET=100: every published port is 100 higher"
  else
    fail "PORT_OFFSET=100 doesn't shift every published port by 100 (< wanted, > got):"$'\n'"$(diff <(echo "$wanted") <(echo "$at_hundred") || true)"
  fi

  # 4. A *_PORT set in the shell wins.
  if grep -qx 'gateway/80=9999' <<<"$(published PORT_OFFSET=100 GATEWAY_PORT=9999 2>/dev/null || true)"; then
    ok "GATEWAY_PORT set in the shell wins over PORT_OFFSET"
  else
    fail "GATEWAY_PORT=9999 with PORT_OFFSET=100 doesn't publish the gateway on 9999"
  fi
fi

# 5. Documented.
if grep -q 'PORT_OFFSET' "$ENV_FILE"; then
  ok ".env.example mentions PORT_OFFSET"
else
  fail ".env.example doesn't mention PORT_OFFSET"
fi
if grep -q 'PORT_OFFSET=' "$README" && grep -qE 'docker compose -p [a-z0-9-]+' "$README" && grep -qF 'ports.sh' "$README"; then
  ok "README.md shows PORT_OFFSET, ports.sh and docker compose -p"
else
  fail "README.md doesn't show PORT_OFFSET with ports.sh and docker compose -p"
fi

if [[ $failures -gt 0 ]]; then
  printf '%d check(s) failed\n' "$failures" >&2
  exit 1
fi
echo "all port-offset checks passed"
