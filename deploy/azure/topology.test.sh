#!/usr/bin/env bash
# Acceptance check for #23: the production topology on Azure Container Instances, checked offline.
# It compiles deploy/azure/main.bicep to ARM JSON and reads the container group from it, so it needs no
# Azure login and creates nothing.
#
# Run it from anywhere in the repo:  deploy/azure/topology.test.sh
# Needs bash, git, python3 and the Bicep CLI (`az bicep install`, or BICEP=<path to bicep>).
#
# Checks:
#   1. the group holds Caddy, Nginx, MongoDB, RabbitMQ and Vault, and only Caddy has a public port (443)
#   2. restartPolicy Always, and a liveness and a readiness probe on every container
#   3. the CPU and memory requests fit the container-group limit (LIMIT_CPU, LIMIT_MEMORY_GB below)
#   4. every secret is a secure parameter and reaches containers as secureValue, never as value
#   5. MongoDB and Caddy keep their data on Azure Files shares
#   6. MongoDB's init script and Vault's start and seed scripts are the compose files, so there is one source
#   7. the member portal is in the group only when main.bicepparam gets a UI version (UI_IMAGE_TAG), because
#      until the portal has a release there is no image to pull (#198)
set -euo pipefail

# The limit per container group for Linux containers without GPU in most regions, switzerlandnorth included
# (Microsoft Learn: "Resource availability and quota limits for ACI", standard SKU).
LIMIT_CPU=4
LIMIT_MEMORY_GB=16

ROOT=$(git rev-parse --show-toplevel)
cd "$ROOT"

WORK=$(mktemp -d "${TMPDIR:-/tmp}/topology-test.XXXXXX")
trap 'rm -rf "$WORK"' EXIT

if [[ -n ${BICEP:-} ]]; then
  "$BICEP" build deploy/azure/main.bicep --stdout >"$WORK/main.json"
else
  az bicep build --file deploy/azure/main.bicep --stdout >"$WORK/main.json"
fi

# main.bicepparam as deploy.yml compiles it: once without a UI version, once with one. Made-up secrets, as
# the file reads them from the environment and doesn't compile without them.
build_params() {
  (
    for name in DOCKERHUB_USERNAME DOCKERHUB_PULL_TOKEN MONGO_ROOT_PASSWORD BOOKING_DB_PASSWORD \
      MEMBERSHIP_DB_PASSWORD IDENTITY_DB_PASSWORD NOTIFICATION_DB_PASSWORD RABBITMQ_PASSWORD VAULT_DEV_ROOT_TOKEN; do
      export "$name=made-up"
    done
    export JWT_SIGNING_KEY=made-up-signing-key-of-32-characters
    unset UI_IMAGE_TAG
    if [[ -n $1 ]]; then export UI_IMAGE_TAG=$1; fi
    if [[ -n ${BICEP:-} ]]; then
      "$BICEP" build-params deploy/azure/main.bicepparam --stdout
    else
      az bicep build-params --file deploy/azure/main.bicepparam --stdout
    fi
  )
}
build_params "" >"$WORK/params-without-ui.json"
build_params 1.2.3 >"$WORK/params-with-ui.json"

LIMIT_CPU=$LIMIT_CPU LIMIT_MEMORY_GB=$LIMIT_MEMORY_GB python3 -I - "$WORK/main.json" "$ROOT" \
  "$WORK/params-without-ui.json" "$WORK/params-with-ui.json" <<'PY'
import base64, json, os, re, sys

template_path, root, params_without_ui_path, params_with_ui_path = sys.argv[1:5]
main = json.load(open(template_path))
failures = []

def ok(message): print(f"ok: {message}")
def fail(message):
    failures.append(message)
    print(f"FAIL: {message}", file=sys.stderr)

# Find the container group, and the template (with its variables) that declares it.
def find_group(template):
    resources = template.get("resources", [])
    # languageVersion 2.0 templates name their resources: an object instead of a list.
    for resource in resources.values() if isinstance(resources, dict) else resources:
        if resource.get("type") == "Microsoft.ContainerInstance/containerGroups":
            return template, resource
        nested = resource.get("properties", {}).get("template")
        if nested:
            found = find_group(nested)
            if found:
                return found
    return None

found = find_group(main)
if not found:
    print("FAIL: no container group in main.bicep", file=sys.stderr)
    sys.exit(1)
template, group = found
variables = template.get("variables", {})

# Resolve the simple expressions the requests use: [variables('x')], [json('0.25')], numbers.
def resolve(value):
    if isinstance(value, str):
        match = re.fullmatch(r"\[variables\('([^']+)'\)\]", value)
        if match:
            return resolve(variables[match.group(1)])
        match = re.fullmatch(r"\[json\('([^']+)'\)\]", value)
        if match:
            return json.loads(match.group(1))
        return value
    if isinstance(value, dict):
        return {key: resolve(item) for key, item in value.items()}
    if isinstance(value, list):
        return [resolve(item) for item in value]
    return value

properties = group["properties"]
# Every container that can be in the group. An optional container makes the list an expression,
# [filter(variables('x'), ...)], and then the candidates are that variable; check 7 checks the filter.
containers_value = properties["containers"]
containers_filter = None
if isinstance(containers_value, str):
    match = re.fullmatch(r"\[filter\(variables\('([^']+)'\), (.*)\)\]", containers_value)
    if not match:
        print(f"FAIL: the group's containers are an expression this check can't read: {containers_value}", file=sys.stderr)
        sys.exit(1)
    containers_value, containers_filter = variables[match.group(1)], match.group(2)
containers = {c["name"]: c["properties"] for c in containers_value}

# 1. Containers and the one public port.
for name in ["caddy", "nginx", "mongodb", "rabbitmq", "vault"]:
    if name in containers:
        ok(f"container {name}")
    else:
        fail(f"no container {name} in the group")
public_ports = [p["port"] for p in properties.get("ipAddress", {}).get("ports", [])]
if public_ports == [443]:
    ok("only 443 is public")
else:
    fail(f"public ports are {public_ports}, expected only 443")
for name, container in containers.items():
    if name != "caddy" and any(p.get("port") == 443 for p in container.get("ports", [])):
        fail(f"{name} claims port 443, which belongs to Caddy")

# 2. Restart policy and probes.
if properties.get("restartPolicy") == "Always":
    ok("restartPolicy Always")
else:
    fail(f"restartPolicy is {properties.get('restartPolicy')}, expected Always")
for name, container in containers.items():
    missing = [probe for probe in ("livenessProbe", "readinessProbe") if not container.get(probe)]
    if missing:
        fail(f"{name} has no {' and no '.join(missing)}")
if all(c.get("livenessProbe") and c.get("readinessProbe") for c in containers.values()):
    ok("every container has a liveness and a readiness probe")

# 3. Requests against the group limit.
cpu = memory = 0.0
for name, container in containers.items():
    requests = resolve(container["resources"])["requests"]
    cpu += float(requests["cpu"])
    memory += float(requests["memoryInGB"])
limit_cpu, limit_memory = float(os.environ["LIMIT_CPU"]), float(os.environ["LIMIT_MEMORY_GB"])
summary = f"{len(containers)} containers request {cpu:g} vCPU and {memory:g} GB (limit {limit_cpu:g} vCPU, {limit_memory:g} GB)"
if cpu <= limit_cpu and memory <= limit_memory:
    ok(summary)
else:
    fail(summary)

# 4. Secrets: secure parameters, passed as secureValue.
secret_name = re.compile(r"(PASS|TOKEN|SECRET|SIGNING_?KEY|_KEY$)", re.IGNORECASE)
failures_before = len(failures)
for name, parameter in main.get("parameters", {}).items():
    if secret_name.search(name) and parameter.get("type") != "securestring":
        fail(f"main.bicep parameter {name} looks like a secret but isn't @secure()")
for name, container in containers.items():
    for variable in container.get("environmentVariables", []):
        if secret_name.search(variable["name"]) and "value" in variable:
            fail(f"{name}: {variable['name']} is passed as value; a secret must be a secureValue")
if len(failures) == failures_before:
    ok("every secret is a secure parameter and a secureValue")

# 5. Azure Files shares.
volumes = {v["name"]: v for v in properties.get("volumes", [])}
def mounts(container_name):
    return {m["name"]: m["mountPath"] for m in containers.get(container_name, {}).get("volumeMounts", [])}
for container_name, path in [("mongodb", "/data/db"), ("caddy", "/data")]:
    share = next((v for v, p in mounts(container_name).items() if p == path), None)
    if share and "azureFile" in volumes.get(share, {}):
        ok(f"{container_name} keeps {path} on the Azure Files share {share}")
    else:
        fail(f"{container_name} has no Azure Files share at {path}")

# 6. One source for the scripts: the secret volumes hold the compose files byte for byte.
def secret_files(container_name):
    files = {}
    for volume_name, path in mounts(container_name).items():
        for file_name, content in volumes.get(volume_name, {}).get("secret", {}).items():
            # loadFileAsBase64() compiles to a variable that holds the file's base64 text.
            files[f"{path}/{file_name}"] = resolve(content)
    return files
expected = {
    "mongodb": {"/docker-entrypoint-initdb.d/01-service-users.js": "deploy/compose/mongodb/init/01-service-users.js"},
    "vault": {"/vault/config/start.sh": "deploy/compose/vault/start.sh",
              "/vault/config/seed.sh": "deploy/compose/vault/seed.sh"},
}
for container_name, wanted in expected.items():
    files = secret_files(container_name)
    for mount_path, source in wanted.items():
        content = files.get(mount_path)
        if content is None or content.startswith("["):
            fail(f"{container_name}: {mount_path} isn't mounted from {source}")
        elif base64.b64decode(content) == open(os.path.join(root, source), "rb").read():
            ok(f"{container_name}: {mount_path} is {source}")
        else:
            fail(f"{container_name}: {mount_path} differs from {source}")

# 7. The member portal only with a UI version. The filter drops exactly that container unless
#    includeMemberPortal is true, and main.bicepparam sets includeMemberPortal from UI_IMAGE_TAG.
if "member-portal" in containers:
    ok("container member-portal (when it has a release)")
else:
    fail("no container member-portal in the group")
expected_filter = "lambda('c', or(parameters('includeMemberPortal'), not(equals(lambdaVariables('c').name, 'member-portal'))))"
if containers_filter == expected_filter:
    ok("member-portal is dropped from the group unless includeMemberPortal is true")
else:
    fail(f"the group's containers should be filtered with {expected_filter}, got {containers_filter}")
def bicepparam(path):
    return {name: p.get("value") for name, p in json.loads(json.load(open(path))["parametersJson"])["parameters"].items()}
without_ui, with_ui = bicepparam(params_without_ui_path), bicepparam(params_with_ui_path)
if without_ui.get("includeMemberPortal") is False:
    ok("main.bicepparam without UI_IMAGE_TAG: no member portal")
else:
    fail(f"main.bicepparam without UI_IMAGE_TAG sets includeMemberPortal to {without_ui.get('includeMemberPortal')}, expected false")
if with_ui.get("includeMemberPortal") is True and with_ui.get("uiImageTag") == "1.2.3":
    ok("main.bicepparam with UI_IMAGE_TAG=1.2.3: the member portal 1.2.3")
else:
    fail(f"main.bicepparam with UI_IMAGE_TAG=1.2.3 sets includeMemberPortal {with_ui.get('includeMemberPortal')} "
         f"and uiImageTag {with_ui.get('uiImageTag')}, expected true and 1.2.3")

if failures:
    print(f"\n{len(failures)} check(s) failed", file=sys.stderr)
    sys.exit(1)
print("\nall checks passed")
PY
