#!/bin/sh
# Starts Vault in dev mode and seeds it (#17). Dev-mode Vault keeps everything in memory, so every start
# begins empty: this script starts the server, waits until it answers, runs seed.sh, and then marks the
# container ready for the healthcheck. A service that depends on Vault being healthy therefore always
# finds its secrets. The same script runs in Azure, where a one-shot seed container would be restarted
# forever by the container group's restart policy.
set -eu

READY_FILE=/tmp/vault-seeded
rm -f "$READY_FILE"

vault server -dev &
server=$!
trap 'kill -TERM "$server" 2>/dev/null' TERM INT

until vault status >/dev/null 2>&1; do
  if ! kill -0 "$server" 2>/dev/null; then
    echo "start: vault server exited before it was ready" >&2
    exit 1
  fi
  sleep 1
done

/bin/sh /vault/config/seed.sh
touch "$READY_FILE"
echo "start: Vault is seeded and ready"

wait "$server"
