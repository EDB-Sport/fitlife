#!/usr/bin/env bash
# Checks the Azure Postman environment against the smoke collection (issues #360, #371). A run that uses only the
# environment file (Newman with --environment and baseUrl) failed with ENOTFOUND once because a variable the
# collection reads was missing. Every variable the collection reads must come from somewhere:
#   - the environment file, or
#   - the run: baseUrl (the Bicep output) and medlemPassword (the secret DEMO_MEDLEM_PASSWORD).
#     (memberPortal, which deploy.yml also sets, is read with pm.variables.get, not as {{memberPortal}}.)
# The password is never in the environment file, and the file's account is one main.bicepparam's Identity seeds.
# The services are the ones in main.bicepparam: the collection asks /api/<context>/health of each.
# Usage: tools/ci/postman-azure-environment.test.sh   (from the repo root, or anywhere in the repo; exit 0 = all pass)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2

python3 -I - \
  tests/postman/environments/azure.postman_environment.json \
  tests/postman/smoke/thin-slice.postman_collection.json \
  deploy/azure/main.bicepparam <<'PY'
import json
import re
import sys

environment_path, collection_path, bicepparam_path = sys.argv[1], sys.argv[2], sys.argv[3]
failures = []

def check(ok, message):
    print(("PASS  " if ok else "FAIL  ") + message)
    if not ok:
        failures.append(message)

with open(environment_path, encoding="utf-8") as f:
    environment = json.load(f)
with open(collection_path, encoding="utf-8") as f:
    collection_text = f.read()
with open(bicepparam_path, encoding="utf-8") as f:
    bicepparam = f.read()

# The variables the smoke collection reads, e.g. {{medlemEmail}}
used = set(re.findall(r"\{\{(\w+)\}\}", collection_text))
values = {v.get("key"): v for v in environment.get("values", [])}
from_the_run = {"baseUrl", "medlemPassword"}
missing = sorted(name for name in used if name not in values and name not in from_the_run)
check(not missing, "the azure environment file has every variable the collection reads" + (f" (missing: {', '.join(missing)})" if missing else ""))
check("baseUrl" in values, "the azure environment file has baseUrl, which deploy.yml replaces with the Bicep output")
disabled = sorted(name for name in used if name in values and values[name].get("enabled") is not True)
check(not disabled, "every variable the collection reads is enabled in the azure environment file")
check("medlemPassword" in used and "medlemPassword" not in values,
      "the collection reads {{medlemPassword}}, and the environment file doesn't carry it: it comes from the secret at run time")
check(not any("password" in key.lower() for key in values), "the azure environment file has no password")
check("serviceBasePath" not in used and "serviceBasePath" not in values,
      "serviceBasePath is gone: each service has its own path, and the Bicep output is removed")

# One health check per service of main.bicepparam, at /api/<context>/health.
contexts = re.findall(r"^\s*context:\s*'([^']+)'", bicepparam, re.MULTILINE)
check(len(contexts) > 0, "main.bicepparam lists its services")
for context in contexts:
    check(f"{{{{baseUrl}}}}/api/{context}/health" in collection_text, f"the smoke collection asks /api/{context}/health")

# The Medlem demo account: the address the Identity seed gives it (compose and Identity's own tests use the same).
medlem_email = (values.get("medlemEmail") or {}).get("value")
check(medlem_email == "medlem@fitlife.example", "the azure environment file has the Medlem demo account's e-mail address")
check("/api/identity/tokens" in collection_text, "the smoke collection logs in with POST /api/identity/tokens")

sys.exit(1 if failures else 0)
PY
