#!/usr/bin/env bash
# Tests for tools/ci/mock-mappings.sh (issue #279): the local mock server in compose serves the saved examples of
# tests/postman/mocks/*.mock.postman_collection.json, picked the way the Postman Mock Server picks them, and the
# committed WireMock mappings can't drift from the collections. Also checks where each environment points.
# Usage: tools/ci/mock-mappings.test.sh   (exit code 0 = every case passes; needs jq)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)
script="$here/mock-mappings.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

check() { # check <description> <command...>
  if "${@:2}"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1"
    echo "      ran: ${*:2}"
    sed 's/^/      out: /' <<<"${out:-}"
    failures=$((failures + 1))
  fi
}

# A collection with one request and two saved examples on one path, and a second request whose examples are on two paths.
cat >"$work/shop.mock.postman_collection.json" <<'JSON'
{"info": {"name": "shop (mock)"}, "item": [
  {"name": "Create order", "request": {"method": "POST", "url": {"raw": "{{url}}/orders"}}, "response": [
    {"name": "order-created", "code": 201, "originalRequest": {"method": "POST", "url": {"raw": "{{url}}/orders"}},
     "header": [{"key": "Content-Type", "value": "application/json"}, {"key": "Location", "value": "/orders/1"}],
     "body": "{\"id\": 1}"},
    {"name": "order-invalid", "code": 400, "originalRequest": {"method": "POST", "url": {"raw": "{{url}}/orders"}},
     "header": [{"key": "Content-Type", "value": "application/json"}, {"key": "X-Off", "value": "x", "disabled": true}],
     "body": "{\"error\": \"invalid\"}"}
  ]},
  {"name": "Get order", "request": {"method": "GET", "url": {"raw": "{{url}}/orders/1"}}, "response": [
    {"name": "order-found", "code": 200, "originalRequest": {"method": "GET", "url": {"raw": "{{url}}/orders/1?expand=lines"}}, "header": [], "body": "{}"},
    {"name": "order-not-found", "code": 404, "originalRequest": {"method": "GET", "url": {"raw": "{{url}}/orders/0"}}, "header": [], "body": ""}
  ]}
]}
JSON

out=$("$script" "$work/shop.mock.postman_collection.json" 2>&1)
status=$?
check "a collection becomes WireMock mappings" test "$status" = 0
mappings=$(jq -c '.mappings[]' <<<"$out" 2>/dev/null)

# stub <jq filter on one mapping>: true when exactly one mapping matches
stub() { [ "$(jq -c "select($1)" <<<"$mappings" | wc -l | tr -d ' ')" = 1 ]; }

check "x-mock-response-name picks an example by its name" \
  stub '.request.method == "POST" and .request.urlPath == "/orders" and .request.headers["x-mock-response-name"].equalTo == "order-invalid" and .response.status == 400'
check "x-mock-response-code picks an example by its status" \
  stub '.request.urlPath == "/orders" and .request.headers["x-mock-response-code"].equalTo == "400" and .response.status == 400'
check "a header match comes before the default answer" \
  stub '.request.headers["x-mock-response-name"].equalTo == "order-created" and .priority < 5'
check "without a header, the first example of a path is the answer" \
  stub '.request.urlPath == "/orders" and (.request.headers | not) and .response.status == 201'
check "a second example on the same path is not a default answer" \
  bash -c '! jq -e "select(.request.urlPath == \"/orders\" and (.request.headers | not) and .response.status == 400)" <<<"$1" >/dev/null' _ "$mappings"
check "an example's own path is used, not its request's" \
  stub '.request.method == "GET" and .request.urlPath == "/orders/0" and (.request.headers | not) and .response.status == 404'
check "the query string is not part of the path" \
  stub '.request.urlPath == "/orders/1" and (.request.headers | not) and .response.status == 200'
check "the example's body is served as it is" \
  stub '.request.headers["x-mock-response-name"].equalTo == "order-created" and .response.body == "{\"id\": 1}"'
check "the example's headers are served, Location too" \
  stub '.request.headers["x-mock-response-name"].equalTo == "order-created" and .response.headers.Location == "/orders/1"'
check "a disabled header is left out" \
  bash -c '! jq -e "select(.response.headers[\"X-Off\"])" <<<"$1" >/dev/null' _ "$mappings"

out=$("$script" 2>&1)
status=$?
check "no collection: usage and exit 2" test "$status" = 2

# The committed mappings are what the script makes of today's collections (#279: the two can't drift).
for collection in "$root"/tests/postman/mocks/*.mock.postman_collection.json; do
  name=$(basename "$collection" .mock.postman_collection.json)
  committed="$root/deploy/compose/mock-server/mappings/$name.json"
  out=$(diff <("$script" "$collection") "$committed" 2>&1)
  check "deploy/compose/mock-server/mappings/$name.json matches mocks/$name.mock (regenerate: tools/ci/mock-mappings.sh --write)" test -z "$out"
done
out=""

# Where each environment points (#279): ci at the local mock, local and azure at the public Postman Mock Server.
env_value() { jq -r --arg key "$2" '.values[] | select(.key == $key) | .value' "$root/tests/postman/environments/$1.postman_environment.json"; }
for key in $(jq -r '.values[].key | select(endswith("MockUrl"))' "$root/tests/postman/environments/ci.postman_environment.json"); do
  check "environments/ci's $key is the local mock server" test "$(env_value ci "$key")" = "http://localhost:8090"
  for public in local azure; do
    # The payment provider has no public Postman Mock Server yet (tests/postman/README.md), so its key may be missing there
    check "environments/$public's $key stays the public Postman Mock Server (or isn't set yet)" \
      bash -c '[[ -z $1 || $1 == https://*.mock.pstmn.io ]]' _ "$(env_value "$public" "$key")"
  done
done
check "environments/ci has a paymentProviderMockUrl" test -n "$(env_value ci paymentProviderMockUrl)"
check "membership-svc in compose charges through the local mock server" \
  grep -qE '^      PaymentProvider__BaseUrl: http://mock-server:8080( |$)' "$root/deploy/compose/services.yml"
check "the mock server is pinned to a full version with a health check" \
  bash -c 'grep -qE "^    image: wiremock/wiremock:[0-9]+\.[0-9]+\.[0-9]+" "$1" && grep -qF "__admin/health" "$1"' _ "$root/deploy/compose/infrastructure.yml"

exit "$failures"
