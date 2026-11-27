#!/usr/bin/env bash
# compose-azure-env: every environment key a service has in deploy/compose/services.yml is in its container in
# deploy/azure/modules/containers.bicep too, or on the local-only list below (issue #524). So compose and Azure can't
# drift apart unnoticed (#519 was such a drift). Step 8 of tools/ci/deploy-check.sh.
# Usage: tools/ci/compose-azure-env.sh [services.yml containers.bicep main.bicepparam]   (default: the repo's files)
# Compared: the services that have a container in Azure (the thin slice). The gateway is Azure's "nginx" container, and
# '${serviceContext}-svc' is the context main.bicepparam sets. A service with no Azure container is named and skipped.
# Fails, naming the service and key, for a compose key the container lacks, and for a local-only entry that matches no
# compose key (so the list can't go stale). COMPOSE_AZURE_LOCAL_ONLY replaces the list (tests). Needs python3.
# Tests: tools/ci/compose-azure-env.test.sh
set -uo pipefail

# The local-only list: one entry per line, "<compose service or *>:<key or glob> <reason>".
# A real gap in Azure is listed here with its issue, so deploy-check stays green until that issue closes it.
LOCAL_ONLY='
*:Loki__Url Azure runs no Loki container, so there is no address to give (only notification-svc sets one)
booking-svc:MongoDb__* #519: Azure Booking has no MongoDb__Host or MongoDb__Port yet
booking-svc:Seed__Enabled #386: the demo class schedule in Azure
booking-svc:WaitlistClosing__Enabled #386: Booking with its compose settings in Azure
booking-svc:Membership__BaseUrl the thin slice has no membership-svc in Azure (#386)
member-portal:Gateway__BaseUrl #599: a real gap, the portal in Azure has no gateway address yet
'

root=$(cd "$(dirname "$0")/../.." && pwd)
services=${1:-$root/deploy/compose/services.yml}
bicep=${2:-$root/deploy/azure/modules/containers.bicep}
bicepparam=${3:-$root/deploy/azure/main.bicepparam}
local_only=${COMPOSE_AZURE_LOCAL_ONLY-$LOCAL_ONLY}

for file in "$services" "$bicep" "$bicepparam"; do
  [ -f "$file" ] || {
    echo "FAIL no such file: $file"
    exit 1
  }
done

python3 - "$services" "$bicep" "$bicepparam" "$local_only" <<'PY'
import fnmatch, re, sys

services_file, bicep_file, bicepparam_file, local_only_text = sys.argv[1:5]
AZURE_NAME = {"gateway": "nginx"}

# compose: service -> its keys under environment:
compose, service, in_env = {}, None, False
for line in open(services_file, encoding="utf-8"):
    if m := re.match(r"^  ([A-Za-z0-9_-]+):\s*$", line):
        service, in_env = m.group(1), False
        compose[service] = []
    elif re.match(r"^    environment:\s*$", line):
        in_env = True
    elif re.match(r"^    \S", line):
        in_env = False
    elif in_env and service and (m := re.match(r"^      ([A-Za-z0-9_]+):", line)):
        compose[service].append(m.group(1))

context = re.search(r"param serviceContext = '([^']+)'", open(bicepparam_file, encoding="utf-8").read())
context = context.group(1) if context else ""

# bicep: container name -> the names in its environmentVariables
azure, container, in_env, env_indent = {}, None, False, 0
for line in open(bicep_file, encoding="utf-8"):
    if m := re.match(r"^    name: '([^']+)'", line):
        container, in_env = m.group(1).replace("${serviceContext}", context), False
        azure[container] = set()
    elif m := re.match(r"^(\s*)environmentVariables: \[", line):
        in_env, env_indent = True, len(m.group(1))
    elif in_env and re.match(r"^\s{%d}\]" % env_indent, line):
        in_env = False
    elif in_env and container and (m := re.match(r"^\s*name: '([A-Za-z0-9_]+)'", line)):
        azure[container].add(m.group(1))

entries = []
for line in local_only_text.splitlines():
    if line.strip():
        pattern, _, reason = line.strip().partition(" ")
        where, _, key = pattern.partition(":")
        entries.append((where, key, reason.strip(), pattern))
used = set()

failed = 0
for service, keys in compose.items():
    name = AZURE_NAME.get(service, service)
    if name not in azure:
        print(f"{service}: no container in Azure, skipped")
        continue
    print(f"{service} (Azure: {name}): {len(keys)} compose keys")
    for key in keys:
        if key in azure[name]:
            continue
        match = next((e for e in entries if e[0] in ("*", service) and fnmatch.fnmatchcase(key, e[1])), None)
        if match:
            used.add(match[3])
            print(f"  local only: {service} {key} ({match[2]})")
        else:
            failed += 1
            print(f"FAIL {service}: {key} is in compose but not in Azure's {name} container. "
                  "Do add it to deploy/azure/modules/containers.bicep, or to the local-only list in tools/ci/compose-azure-env.sh with a reason.")

for where, key, reason, pattern in entries:
    if pattern not in used and not any(
        AZURE_NAME.get(s, s) in azure and where in ("*", s) and any(fnmatch.fnmatchcase(k, key) and k not in azure[AZURE_NAME.get(s, s)] for k in ks)
        for s, ks in compose.items()
    ):
        failed += 1
        print(f"FAIL local-only entry {pattern} matches no compose key that Azure lacks. Do remove it from tools/ci/compose-azure-env.sh.")

if failed == 0:
    print("ok: every compose key of the services in Azure is in their containers or on the local-only list")
sys.exit(1 if failed else 0)
PY
