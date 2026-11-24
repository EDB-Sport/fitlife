#!/bin/sh
# Writes the stack's secrets into Vault (#17, course rule #9). vault/start.sh runs it each time the
# Vault container starts, because dev-mode Vault keeps everything in memory and starts empty.
#
# Each service gets one secret, secret/<name>-svc, for every name in SERVICE_DATABASES (e.g. booking-svc).
# The keys are configuration keys, so a service can load the secret into IConfiguration as it is:
#   MongoDb:Password    from <NAME>_DB_PASSWORD, for the service's own database user
#   RabbitMq:Username   from RABBITMQ_USERNAME
#   RabbitMq:Password   from RABBITMQ_PASSWORD
#   Jwt:SigningKey      from JWT_SIGNING_KEY
# VAULT_ADDR and VAULT_TOKEN tell the vault CLI where to write. `kv put` replaces the secret, so running
# the script again is safe.
set -eu

require() {
  eval "value=\${$1:-}"
  if [ -z "$value" ]; then
    echo "seed: $1 is not set" >&2
    exit 1
  fi
}

for variable in VAULT_ADDR VAULT_TOKEN SERVICE_DATABASES RABBITMQ_USERNAME RABBITMQ_PASSWORD JWT_SIGNING_KEY; do
  require "$variable"
done

for service in $SERVICE_DATABASES; do
  password_variable="$(echo "$service" | tr '[:lower:]' '[:upper:]')_DB_PASSWORD"
  require "$password_variable"
  eval "db_password=\$$password_variable"

  vault kv put -mount=secret "$service-svc" \
    "MongoDb:Password=$db_password" \
    "RabbitMq:Username=$RABBITMQ_USERNAME" \
    "RabbitMq:Password=$RABBITMQ_PASSWORD" \
    "Jwt:SigningKey=$JWT_SIGNING_KEY" >/dev/null
  echo "seed: wrote secret/$service-svc"
done
