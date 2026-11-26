#!/usr/bin/env bash
# Checks that the Azure Postman environment carries serviceBasePath (issue #360). The smoke collection calls
# {{baseUrl}}{{serviceBasePath}}/health, and a run that uses only the environment file failed with ENOTFOUND
# because the variable was missing. The value must be the one the Bicep output gives: /api/<serviceContext>.
# Usage: tools/ci/postman-azure-environment.test.sh   (from the repo root, or anywhere in the repo; exit 0 = all pass)
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

# The variables the smoke collection reads, e.g. {{serviceBasePath}}
used = sorted(set(re.findall(r"\{\{(\w+)\}\}", collection_text)))
check("serviceBasePath" in used, "the smoke collection reads {{serviceBasePath}}")

values = {v.get("key"): v for v in environment.get("values", [])}
service_base_path = values.get("serviceBasePath")
check(service_base_path is not None, "the azure environment file has serviceBasePath")
if service_base_path is not None:
    check(service_base_path.get("enabled") is True, "serviceBasePath is enabled in the azure environment file")

# The Bicep output is '/api/${serviceContext}', and main.bicepparam sets serviceContext
match = re.search(r"^param\s+serviceContext\s*=\s*'([^']+)'", bicepparam, re.MULTILINE)
context = match.group(1) if match else None
check(context is not None, "main.bicepparam sets serviceContext")
if context is not None and service_base_path is not None:
    expected = f"/api/{context}"
    check(service_base_path.get("value") == expected,
          f"serviceBasePath in the azure environment file is {expected}, the Bicep output for serviceContext '{context}'")

if failures:
    print(f"\n{len(failures)} check(s) failed")
    sys.exit(1)
print("\nall checks pass")
PY
