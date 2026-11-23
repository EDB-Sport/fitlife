#!/usr/bin/env bash
# Acceptance check for #17: Vault in dev mode in the compose stack, seeded from the .env values.
#
# Run it from anywhere in the repo:  deploy/compose/vault/seed.test.sh
# Needs Docker with Compose v2. It starts only Vault and the seed job, in a separate compose project
# (fitlife-vault-test) so it doesn't touch your own stack, and removes them again at the end.
#
# Checks:
#   1. the Vault image is pinned, and no token or secret is written in the compose files
#   2. every service in SERVICE_DATABASES has its secrets at secret/<name>-svc, with the values from the
#      env file: MongoDb:Password, RabbitMq:Username, RabbitMq:Password, Jwt:SigningKey
#   3. the seed job can run again on a seeded Vault (a restart of the stack runs it again)
set -euo pipefail

ROOT=$(git rev-parse --show-toplevel)
COMPOSE_DIR=$ROOT/deploy/compose
ENV_FILE=$COMPOSE_DIR/.env.example
PROJECT=fitlife-vault-test

failures=0
fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$*" >&2
}
ok() { printf 'ok: %s\n' "$*"; }

# The same isolation as tools/ci/deploy-check.sh: only .env.example, nothing from your shell or .env.
compose() {
  local keep=(PATH="$PATH" HOME="$HOME")
  local var
  for var in DOCKER_HOST DOCKER_CONTEXT DOCKER_CONFIG DOCKER_CERT_PATH DOCKER_TLS_VERIFY; do
    if [[ -n ${!var:-} ]]; then keep+=("$var=${!var}"); fi
  done
  env -i "${keep[@]}" docker compose --project-directory "$COMPOSE_DIR" -p "$PROJECT" \
    --env-file "$ENV_FILE" -f "$COMPOSE_DIR/infrastructure.yml" "$@"
}

# A value from the env file, e.g. env_value RABBITMQ_PASSWORD.
env_value() {
  sed -n "s/^$1=//p" "$ENV_FILE"
}

cleanup() { compose down -v --remove-orphans >/dev/null 2>&1 || true; }
trap cleanup EXIT

# 1. Pinned image, nothing secret in the compose files.
vault_image=$(compose config --format json | jq -r '.services.vault.image // empty')
if [[ -z $vault_image ]]; then
  fail "no vault service in infrastructure.yml"
elif [[ $vault_image != hashicorp/vault:[0-9]* ]]; then
  fail "the Vault image isn't pinned to a version: $vault_image"
else
  ok "Vault image $vault_image"
fi
if grep -nE 'VAULT_(DEV_ROOT_)?TOKEN(_ID)?: *[^$ ]' "$COMPOSE_DIR"/*.yml; then
  fail "a Vault token is written in a compose file; it must come from .env"
else
  ok "no Vault token in the compose files"
fi

# 2. Start Vault and the seed job, then read every service's secrets back.
if ! compose up -d --wait vault vault-seed; then
  fail "vault and vault-seed didn't start"
  compose logs vault vault-seed >&2 || true
  exit 1
fi

read_secret() { # read_secret <path> <key>
  compose exec -T vault vault kv get -field="$2" "secret/$1"
}

rabbit_user=$(env_value RABBITMQ_USERNAME)
rabbit_password=$(env_value RABBITMQ_PASSWORD)
jwt_key=$(env_value JWT_SIGNING_KEY)
[[ -n $jwt_key ]] || fail "JWT_SIGNING_KEY is missing from .env.example"

services=$(compose config --format json | jq -r '.services.mongodb.environment.SERVICE_DATABASES')
for service in $services; do
  path="$service-svc"
  db_password=$(env_value "$(printf '%s' "$service" | tr '[:lower:]' '[:upper:]')_DB_PASSWORD")
  for pair in "MongoDb:Password=$db_password" "RabbitMq:Username=$rabbit_user" \
    "RabbitMq:Password=$rabbit_password" "Jwt:SigningKey=$jwt_key"; do
    key=${pair%%=*}
    want=${pair#*=}
    got=$(read_secret "$path" "$key" 2>/dev/null || true)
    if [[ $got == "$want" && -n $want ]]; then
      ok "secret/$path $key"
    else
      fail "secret/$path $key is '${got}', expected the value from .env.example"
    fi
  done
done

# 3. Seeding again works, so `docker compose up` on an existing stack doesn't fail.
if compose run --rm -T vault-seed >/dev/null; then
  ok "the seed job runs again on a seeded Vault"
else
  fail "the seed job fails when Vault is already seeded"
fi

if ((failures > 0)); then
  printf '\n%d check(s) failed\n' "$failures" >&2
  exit 1
fi
printf '\nall checks passed\n'
