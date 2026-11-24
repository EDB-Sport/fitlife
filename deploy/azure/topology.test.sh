#!/usr/bin/env bash
# Acceptance check for #23: the production topology on Azure Container Instances, checked offline.
# Check 7 is #17: the service reads its secret from Vault in the group.
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
set -euo pipefail

# The limit per container group for Linux containers without GPU in most regions, northeurope included
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

LIMIT_CPU=$LIMIT_CPU LIMIT_MEMORY_GB=$LIMIT_MEMORY_GB python3 -I - "$WORK/main.json" "$ROOT" <<'PY'
import base64, json, os, re, sys

template_path, root = sys.argv[1], sys.argv[2]
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
containers = {c["name"]: c["properties"] for c in properties["containers"]}

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

# 7. The service reads its secret from Vault in the group (#17): the address on the group's localhost,
#    the token as a secureValue. Compose gives it the same settings (deploy/compose/services.yml).
infrastructure = {"caddy", "nginx", "member-portal", "mongodb", "rabbitmq", "vault"}
for name, container in containers.items():
    if name in infrastructure:
        continue
    variables = {v["name"]: v for v in container.get("environmentVariables", [])}
    address = str(resolve(variables.get("Vault__Address", {}).get("value", "")))
    if "127.0.0.1" in address and "secureValue" in variables.get("Vault__Token", {}):
        ok(f"{name} reads its secret from Vault on the group's localhost")
    else:
        fail(f"{name} has no Vault__Address on 127.0.0.1 and Vault__Token as a secureValue")

if failures:
    print(f"\n{len(failures)} check(s) failed", file=sys.stderr)
    sys.exit(1)
print("\nall checks passed")
PY
