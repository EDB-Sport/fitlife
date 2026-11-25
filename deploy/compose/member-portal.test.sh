#!/usr/bin/env bash
# Acceptance check for #204: the member portal runs as its own container in the compose stack, behind the gateway.
#
# Run it from anywhere in the repo:  deploy/compose/member-portal.test.sh
# Needs Docker with Compose v2 and jq. It only reads the configuration (docker compose config) and starts
# nothing: the api-tests check proves the container gets healthy, as `docker compose up --wait` needs it.
#
# Checks:
#   1. services.yml has a member-portal service built from the repo root with src/MemberPortal.Web/Dockerfile,
#      image fitlife/memberportal:local, and a healthcheck on http://127.0.0.1:8080/health, like booking-svc
#   2. its environment sets Gateway__BaseUrl and Loki__Url, and it has no depends_on
#   3. the gateway's MEMBER_PORTAL_URL points at that service
#   4. appsettings.Development.json has Gateway:BaseUrl for `dotnet run` next to the stack
#   5. deploy/compose/README.md says the portal runs in the stack and where to open it
set -euo pipefail

ROOT=$(git rev-parse --show-toplevel)
COMPOSE_DIR=$ROOT/deploy/compose
ENV_FILE=$COMPOSE_DIR/.env.example

failures=0
fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$*" >&2
}
ok() { printf 'ok: %s\n' "$*"; }

# expect <description> <actual> <expected>
expect() {
  if [[ $2 == "$3" ]]; then ok "$1"; else fail "$1: expected '$3', got '${2:-<none>}'"; fi
}

# The same isolation as tools/ci/deploy-check.sh: only .env.example, nothing from your shell or .env.
compose() {
  local keep=(PATH="$PATH" HOME="$HOME")
  local var
  for var in DOCKER_HOST DOCKER_CONTEXT DOCKER_CONFIG DOCKER_CERT_PATH DOCKER_TLS_VERIFY; do
    if [[ -n ${!var:-} ]]; then keep+=("$var=${!var}"); fi
  done
  env -i "${keep[@]}" docker compose --project-directory "$COMPOSE_DIR" --env-file "$ENV_FILE" \
    -f "$COMPOSE_DIR/services.yml" "$@"
}

config=$(compose config --format json)
portal=$(jq -c '.services["member-portal"] // empty' <<<"$config")

# 1. Build, image and healthcheck, like booking-svc.
if [[ -z $portal ]]; then
  fail "services.yml has no member-portal service"
  portal='{}'
else
  ok "services.yml has a member-portal service"
fi
expect "built from the repo root" "$(jq -r '.build.context // empty' <<<"$portal")" "$ROOT"
expect "with src/MemberPortal.Web/Dockerfile" "$(jq -r '.build.dockerfile // empty' <<<"$portal")" "src/MemberPortal.Web/Dockerfile"
expect "image fitlife/memberportal:local" "$(jq -r '.image // empty' <<<"$portal")" "fitlife/memberportal:local"
healthcheck=$(jq -r '(.healthcheck.test // []) | join(" ")' <<<"$portal")
if [[ $healthcheck == *"http://127.0.0.1:8080/health"* ]]; then
  ok "healthcheck on http://127.0.0.1:8080/health"
else
  fail "healthcheck on http://127.0.0.1:8080/health: got '${healthcheck:-<none>}'"
fi

# 2. Environment, and no depends_on, so services.yml still starts on its own.
expect "Gateway__BaseUrl is the gateway inside the stack" "$(jq -r '.environment.Gateway__BaseUrl // empty' <<<"$portal")" "http://gateway:80"
expect "Loki__Url is Loki in monitoring.yml" "$(jq -r '.environment.Loki__Url // empty' <<<"$portal")" "http://loki:3100"
expect "no depends_on" "$(jq -r 'has("depends_on")' <<<"$portal")" "false"

# 3. The gateway sends every other path to it.
expect "the gateway's MEMBER_PORTAL_URL is the member-portal service" \
  "$(jq -r '.services.gateway.environment.MEMBER_PORTAL_URL // empty' <<<"$config")" "http://member-portal:8080"

# 4. dotnet run next to the stack reaches the gateway on the host.
expect "appsettings.Development.json has Gateway:BaseUrl" \
  "$(jq -r '.Gateway.BaseUrl // empty' "$ROOT/src/MemberPortal.Web/appsettings.Development.json")" "http://localhost:8080"

# 5. The README says where to open it.
if grep -qiE 'member portal.*http://localhost:8080/' "$COMPOSE_DIR/README.md"; then
  ok "README says where to open the member portal"
else
  fail "deploy/compose/README.md doesn't say that the member portal runs in the stack at http://localhost:8080/"
fi

echo
if [[ $failures -gt 0 ]]; then
  echo "member-portal.test.sh: $failures check(s) failed" >&2
  exit 1
fi
echo "member-portal.test.sh: the member portal runs in the compose stack"
