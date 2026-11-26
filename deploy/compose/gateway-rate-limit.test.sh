#!/usr/bin/env bash
# Acceptance check for #366: the gateway limits log-ins (POST /api/identity/tokens) per client address.
#
# Run it from anywhere in the repo:  deploy/compose/gateway-rate-limit.test.sh
# It only reads files. `nginx -t` on the rendered template is deploy-check's step 3 (tools/ci/deploy-check.sh),
# which renders it with the gateway's pinned image and environment.
#
# Checks:
#   1. the template has a limit_req_zone keyed on $binary_remote_addr, with ${LOGIN_RATE_LIMIT} as its rate
#   2. a location = /api/identity/tokens with limit_req (burst ${LOGIN_RATE_BURST}), routed as /api/identity/ is,
#      and limit_req_status 429. No proxy_set_header in it: that would drop the server-level ones
#   3. Nginx takes the client address from X-Forwarded-For only from 127.0.0.1 (Caddy in Azure)
#   4. LOGIN_RATE_LIMIT and LOGIN_RATE_BURST are set for the gateway in services.yml, .env.example and containers.bicep
#   5. deploy/compose/README.md and deploy/azure/README.md say what is limited and that it answers 429
set -euo pipefail

ROOT=$(git rev-parse --show-toplevel)
TEMPLATE=$ROOT/deploy/compose/nginx/default.conf.template
SERVICES=$ROOT/deploy/compose/services.yml
ENV_FILE=$ROOT/deploy/compose/.env.example
BICEP=$ROOT/deploy/azure/modules/containers.bicep

failures=0
fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$*" >&2
}
ok() { printf 'ok: %s\n' "$*"; }

# has <description> <file> <extended regex>
has() {
  if grep -qE -- "$3" "$2"; then ok "$1"; else fail "$1 (no match for '$3' in ${2#"$ROOT"/})"; fi
}

# The template without comments, so a commented-out line doesn't count.
config=$(sed -e 's/#.*$//' "$TEMPLATE")

# The body of `location = /api/identity/tokens { ... }`, up to its closing brace.
tokens_location=$(awk '
  /location[[:space:]]+=[[:space:]]+\/api\/identity\/tokens[[:space:]]*\{/ { inside = 1; next }
  inside && /^[[:space:]]*\}/ { exit }
  inside { print }
' <<<"$config")

# 1. The zone.
if grep -qE 'limit_req_zone[[:space:]]+\$binary_remote_addr[[:space:]]+zone=login:[0-9]+m[[:space:]]+rate=\$\{LOGIN_RATE_LIMIT\};' <<<"$config"; then
  ok 'limit_req_zone keyed on $binary_remote_addr with rate=${LOGIN_RATE_LIMIT}'
else
  fail 'the template has no "limit_req_zone $binary_remote_addr zone=login:<n>m rate=${LOGIN_RATE_LIMIT};"'
fi

# 2. The location.
if [[ -z $tokens_location ]]; then
  fail "the template has no 'location = /api/identity/tokens'"
else
  ok "the template has 'location = /api/identity/tokens'"
  if grep -qE 'limit_req[[:space:]]+zone=login[[:space:]]+burst=\$\{LOGIN_RATE_BURST\}' <<<"$tokens_location"; then
    ok 'it has limit_req zone=login burst=${LOGIN_RATE_BURST}'
  else
    fail 'it has no "limit_req zone=login burst=${LOGIN_RATE_BURST}"'
  fi
  if grep -qE 'proxy_pass[[:space:]]+\$identity_upstream;' <<<"$tokens_location" \
    && grep -qE 'set[[:space:]]+\$identity_upstream[[:space:]]+\$\{IDENTITY_URL\};' <<<"$tokens_location" \
    && grep -qE 'rewrite[[:space:]]+\^/api/identity/\(\.\*\)\$[[:space:]]+/\$1[[:space:]]+break;' <<<"$tokens_location"; then
    ok "it routes to Identity as /api/identity/ does"
  else
    fail "it doesn't route to Identity as /api/identity/ does (set \$identity_upstream, rewrite, proxy_pass)"
  fi
  if grep -qE 'proxy_set_header' <<<"$tokens_location"; then
    fail "it has a proxy_set_header, which drops the server-level ones there"
  else
    ok "it has no proxy_set_header"
  fi
fi
if grep -qE 'limit_req_status[[:space:]]+429;' <<<"$config"; then
  ok "limit_req_status 429"
else
  fail "the template has no 'limit_req_status 429;'"
fi

# 3. The client address behind Caddy, trusted only from 127.0.0.1.
if grep -qE 'set_real_ip_from[[:space:]]+127\.0\.0\.1;' <<<"$config" && grep -qE 'real_ip_header[[:space:]]+X-Forwarded-For;' <<<"$config"; then
  ok "X-Forwarded-For is the client address, only from 127.0.0.1"
else
  fail "the template needs 'set_real_ip_from 127.0.0.1;' and 'real_ip_header X-Forwarded-For;'"
fi
trusted=$({ grep -oE 'set_real_ip_from[[:space:]]+[^;]+;' <<<"$config" || true; } | sed -E 's/set_real_ip_from[[:space:]]+//; s/;$//' | sort -u | tr '\n' ' ')
if [[ -n $trusted && $trusted != "127.0.0.1 " ]]; then
  fail "set_real_ip_from trusts more than 127.0.0.1: $trusted"
fi

# 4. The values.
for var in LOGIN_RATE_LIMIT LOGIN_RATE_BURST; do
  has "services.yml sets $var for the gateway" "$SERVICES" "^[[:space:]]+$var: "
  has ".env.example has $var" "$ENV_FILE" "^$var=[^[:space:]]+"
  has "containers.bicep sets $var for nginx" "$BICEP" "name: '$var'"
done

# 5. The READMEs.
has "deploy/compose/README.md says log-in is limited and answers 429" "$ROOT/deploy/compose/README.md" '/api/identity/tokens.*429|429.*/api/identity/tokens'
has "deploy/azure/README.md says log-in is limited and answers 429" "$ROOT/deploy/azure/README.md" '/api/identity/tokens.*429|429.*/api/identity/tokens'

if ((failures > 0)); then
  printf '\n%d check(s) failed.\n' "$failures" >&2
  exit 1
fi
printf '\nAll checks passed.\n'
