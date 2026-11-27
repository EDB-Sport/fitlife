#!/usr/bin/env bash
# Tests for tools/ci/compose-azure-env.sh (issue #524): every environment key a service has in compose is in its Azure
# container too, or on the script's local-only list. Fixture files, no Docker.
# Usage: tools/ci/compose-azure-env.test.sh   (exit code 0 = every case passes; needs python3)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
script="$here/compose-azure-env.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

check() { # check <description> <command...>
  if "${@:2}"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1"
    echo "      ran: ${*:2}"
    sed 's/^/      out: /' <<<"$out"
    failures=$((failures + 1))
  fi
}
has() { grep -qF -- "$1" <<<"$out"; }
lacks() { ! grep -qF -- "$1" <<<"$out"; }

# compose <booking-svc keys...>: a services.yml with a gateway, booking-svc with those keys, and identity-svc (not in Azure)
compose() {
  {
    echo "services:"
    echo "  gateway:"
    echo "    image: nginx:1.30.7-alpine"
    echo "    environment:"
    echo "      BOOKING_URL: \${BOOKING_URL:-http://booking-svc:8080}"
    echo "  booking-svc:"
    echo "    environment:"
    for key in "$@"; do echo "      $key: x # a comment"; done
    echo "    healthcheck:"
    echo "      test: [\"CMD\", \"true\"]"
    echo "  identity-svc:"
    echo "    environment:"
    echo "      Only__InCompose: x"
  } >"$work/services.yml"
}

cat >"$work/containers.bicep" <<'BICEP'
var containers = [
  {
    name: 'nginx'
    properties: {
      environmentVariables: [
        {
          name: 'BOOKING_URL'
          value: 'http://127.0.0.1:5001'
        }
      ]
      volumeMounts: [
        {
          name: 'nginx-templates'
        }
      ]
    }
  }
  {
    name: '${serviceContext}-svc'
    properties: {
      environmentVariables: [
        {
          name: 'Vault__Address'
          value: 'http://127.0.0.1:8200'
        }
        {
          name: 'Vault__Token'
          secureValue: vaultRootToken
        }
      ]
    }
  }
]
BICEP
echo "param serviceContext = 'booking'" >"$work/main.bicepparam"

# run [local-only list]: runs the script on the fixtures; sets out and status
run() {
  out=$(COMPOSE_AZURE_LOCAL_ONLY="${1-}" "$script" "$work/services.yml" "$work/containers.bicep" "$work/main.bicepparam" 2>&1)
  status=$?
}

compose Vault__Address Vault__Token
run ""
check "a key in both passes" test "$status" = 0
check "the gateway is compared with the nginx container" has "gateway (Azure: nginx)"
check "\${serviceContext}-svc is the bicepparam's context" has "booking-svc (Azure: booking-svc)"
check "a service with no Azure container is skipped and named" has "identity-svc: no container in Azure, skipped"

compose Vault__Address Vault__Token MongoDb__Host
run ""
check "a key the Azure container lacks fails" test "$status" != 0
check "the failure names the service and the key" has "FAIL booking-svc: MongoDb__Host is in compose but not in Azure's booking-svc container"
check "the failure says how to fix it" has "add it to deploy/azure/modules/containers.bicep, or to the local-only list"

run "booking-svc:MongoDb__* #519: not in Azure yet"
check "the same key on the local-only list passes" test "$status" = 0
check "a local-only key is named with its reason" has "local only: booking-svc MongoDb__Host (#519: not in Azure yet)"

run "*:MongoDb__Host the same for every service"
check "a local-only entry for every service (*) passes" test "$status" = 0

run "membership-svc:MongoDb__Host another service"
check "a local-only entry of another service doesn't cover this one" test "$status" != 0

compose Vault__Address Vault__Token
run "booking-svc:Seed__Enabled #386"
check "a local-only entry that matches no compose key fails, so the list can't go stale" test "$status" != 0
check "the stale entry is named" has "FAIL local-only entry booking-svc:Seed__Enabled matches no compose key"

out=$("$script" "$work/nothing.yml" "$work/containers.bicep" "$work/main.bicepparam" 2>&1)
status=$?
check "a missing file fails" test "$status" != 0

# The repo's own files, with the script's own list: develop passes.
out=$("$script" 2>&1)
status=$?
check "the repo's compose and Azure files pass with the local-only list in the script" test "$status" = 0

exit "$failures"
