# shellcheck shell=sh
# Shifts every host port the compose stack publishes by PORT_OFFSET (default 0), so a second stack runs next to
# yours on one Docker host (#585). Compose can't add numbers, so source this before `docker compose`:
#   cd deploy/compose && PORT_OFFSET=100 . ./ports.sh && docker compose -p fitlife-2 up -d --wait
# A *_PORT already set in your shell wins. Shell variables beat .env, so with this sourced .env's *_PORT lines
# are not used. Without it nothing changes: the compose files' own defaults and .env apply, as in CI.
# The defaults are the ones in .env.example; deploy/compose/port-offset.test.sh checks every published port.
PORT_OFFSET=${PORT_OFFSET:-0}
export GATEWAY_PORT="${GATEWAY_PORT:-$((8080 + PORT_OFFSET))}"
export MONGO_HOST_PORT="${MONGO_HOST_PORT:-$((27018 + PORT_OFFSET))}"
export RABBITMQ_HOST_PORT="${RABBITMQ_HOST_PORT:-$((5672 + PORT_OFFSET))}"
export RABBITMQ_MANAGEMENT_HOST_PORT="${RABBITMQ_MANAGEMENT_HOST_PORT:-$((15672 + PORT_OFFSET))}"
export VAULT_HOST_PORT="${VAULT_HOST_PORT:-$((8200 + PORT_OFFSET))}"
export LOKI_HOST_PORT="${LOKI_HOST_PORT:-$((3100 + PORT_OFFSET))}"
echo "ports.sh: PORT_OFFSET=$PORT_OFFSET, gateway on http://localhost:$GATEWAY_PORT (MongoDB $MONGO_HOST_PORT, RabbitMQ $RABBITMQ_HOST_PORT and $RABBITMQ_MANAGEMENT_HOST_PORT, Vault $VAULT_HOST_PORT, Loki $LOKI_HOST_PORT)" >&2
