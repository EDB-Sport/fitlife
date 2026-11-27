#!/usr/bin/env bash
# Acceptance check for #23, #371 and #386: the production topology on Azure Container Instances, checked offline.
# It compiles deploy/azure/main.bicep to ARM JSON, works out the container group the way Azure would (for the
# values main.bicepparam gives), and checks it. It needs no Azure login and creates nothing.
#
# Run it from anywhere in the repo:  deploy/azure/topology.test.sh
# Needs bash, git, python3 and the Bicep CLI (`az bicep install`, or BICEP=<path to bicep>).
#
# Checks:
#   1. the group holds Caddy, Nginx, MongoDB, RabbitMQ and Vault, and only Caddy has a public port (443)
#   2. restartPolicy Always, and a liveness and a readiness probe on every container
#   3. the CPU and memory requests fit the container-group limit (LIMIT_CPU, LIMIT_MEMORY_GB below),
#      and memory is in 0.1 GB steps, the only amounts ACI accepts
#   4. every secret is a secure parameter and reaches containers as secureValue, never as value
#   5. MongoDB and Caddy keep their data on Azure Files shares
#   6. MongoDB's init script and Vault's start and seed scripts are the compose files, so there is one source
#   7. every service reads its secret from Vault in the group (#17): its address on localhost, the token as
#      a secureValue
#   8. the member portal is in the group only when main.bicepparam gets a UI version (UI_IMAGE_TAG), because
#      until the portal has a release there is no image to pull (#198)
#   9. NotificationService (#261): the container notification-svc has every setting of its compose block, on
#      the group's localhost, with /health as both probes, and its database password is a secure parameter
#  10. like the member portal, it is in the group only when main.bicepparam gets NOTIFICATION_IMAGE_TAG
#  11. Booking, Membership and Identity are in the group (#371), each on its own port (the one Nginx routes to),
#      with ASPNETCORE_HTTP_PORTS and both probes on /health
#  12. each of the three gets the settings its block in deploy/compose/services.yml has, with 127.0.0.1 addresses
#      and its secrets as secureValue, and nothing else (only Loki__Url is left out, #277)
#  13. the Nginx container has a value for every ${...} in the template
#  14. the demo accounts' passwords reach Identity from their own secrets, and no password of .env.example is in
#      deploy/azure
#  15. Booking seeds the demo class schedule (#386): booking-svc has the seed setting at "true", under the one key
#      src/BookingService/Program.cs reads, and compose's booking-svc block has the same key
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

# main.bicepparam as deploy.yml compiles it, with the optional versions $1 (UI_IMAGE_TAG) and $2
# (NOTIFICATION_IMAGE_TAG), each left unset when empty. Made-up secrets, as the file reads them from the
# environment and doesn't compile without them. NotificationService's database password and the three demo
# passwords differ from the rest, so checks 9 and 14 can tell which secret reaches which setting.
build_params() {
  (
    for name in DOCKERHUB_USERNAME DOCKERHUB_PULL_TOKEN MONGO_ROOT_PASSWORD BOOKING_DB_PASSWORD \
      MEMBERSHIP_DB_PASSWORD IDENTITY_DB_PASSWORD RABBITMQ_PASSWORD VAULT_DEV_ROOT_TOKEN; do
      export "$name=made-up"
    done
    export JWT_SIGNING_KEY=made-up-signing-key-of-32-characters
    export NOTIFICATION_DB_PASSWORD=made-up-notification-db
    export DEMO_MEDLEM_PASSWORD=made-up-demo-medlem DEMO_TRAENER_PASSWORD=made-up-demo-traener DEMO_ADMIN_PASSWORD=made-up-demo-admin
    unset UI_IMAGE_TAG NOTIFICATION_IMAGE_TAG
    if [[ -n $1 ]]; then export UI_IMAGE_TAG=$1; fi
    if [[ -n $2 ]]; then export NOTIFICATION_IMAGE_TAG=$2; fi
    if [[ -n ${BICEP:-} ]]; then
      "$BICEP" build-params deploy/azure/main.bicepparam --stdout
    else
      az bicep build-params --file deploy/azure/main.bicepparam --stdout
    fi
  )
}
build_params "" "" >"$WORK/params-without-optional.json"
build_params 1.2.3 4.5.6 >"$WORK/params-with-optional.json"
build_params 1.2.3 "" >"$WORK/params-ui-only.json"
build_params "" 4.5.6 >"$WORK/params-notification-only.json"

LIMIT_CPU=$LIMIT_CPU LIMIT_MEMORY_GB=$LIMIT_MEMORY_GB python3 -I - "$WORK/main.json" "$ROOT" \
  "$WORK/params-without-optional.json" "$WORK/params-with-optional.json" \
  "$WORK/params-ui-only.json" "$WORK/params-notification-only.json" <<'PY'
import base64, json, os, re, sys

template_path, root, params_without_path, params_with_path, params_ui_path, params_notification_path = sys.argv[1:7]
main = json.load(open(template_path))
failures = []

def ok(message): print(f"ok: {message}")
def fail(message):
    failures.append(message)
    print(f"FAIL: {message}", file=sys.stderr)

# ---- Working out the container group --------------------------------------------------------------------
# The group's containers are an ARM expression over the parameters (a loop over the services, and a filter for the
# member portal and NotificationService). This evaluates the few functions the compiled template uses, so the checks
# below see the containers Azure would create for the values main.bicepparam gives.

def tokenize(text):
    tokens, i = [], 0
    while i < len(text):
        c = text[i]
        if c.isspace():
            i += 1
        elif c == "'":
            j, out = i + 1, ""
            while True:
                if text[j] == "'":
                    if text[j + 1:j + 2] == "'":
                        out += "'"
                        j += 2
                        continue
                    break
                out += text[j]
                j += 1
            tokens.append(("lit", out))
            i = j + 1
        elif c.isdigit() or (c == "-" and text[i + 1:i + 2].isdigit()):
            number = re.compile(r"-?\d+(\.\d+)?").match(text, i)
            tokens.append(("lit", float(number.group()) if number.group(1) else int(number.group())))
            i = number.end()
        elif c.isalpha() or c == "_":
            name = re.compile(r"[A-Za-z_]\w*(?:\.[A-Za-z_]\w*(?=\())?").match(text, i)
            tokens.append(("id", name.group()))
            i = name.end()
        else:
            tokens.append(("p", c))
            i += 1
    return tokens

def parse_primary(tokens, pos):
    kind, value = tokens[pos]
    if kind == "lit":
        return ("lit", value), pos + 1
    if tokens[pos + 1:pos + 2] == [("p", "(")]:
        args, pos = [], pos + 2
        while tokens[pos] != ("p", ")"):
            arg, pos = parse_expression(tokens, pos)
            args.append(arg)
            if tokens[pos] == ("p", ","):
                pos += 1
        return ("call", value, args), pos + 1
    return ("lit", {"true": True, "false": False, "null": None}[value]), pos + 1

def parse_expression(tokens, pos):
    node, pos = parse_primary(tokens, pos)
    while pos < len(tokens) and tokens[pos] in (("p", "."), ("p", "[")):
        if tokens[pos] == ("p", "."):
            node, pos = ("prop", node, ("lit", tokens[pos + 1][1])), pos + 2
        else:
            index, pos = parse_expression(tokens, pos + 1)
            node, pos = ("prop", node, index), pos + 1  # past the closing ]
    return node, pos

class Thunk:
    """A module parameter: the expression in the calling template, worked out when the module asks for it."""
    def __init__(self, work):
        self.work = work

class Scope:
    """One template with its parameters: the main template, a module, or a user-defined function."""
    def __init__(self, template, params):
        self.template, self.params = template, params
        self.known_params, self.known_variables, self.lambdas, self.copies = {}, {}, {}, {}

    def param(self, name):
        if name not in self.known_params:
            if name not in self.params:
                self.known_params[name] = self.resolve(self.template["parameters"][name]["defaultValue"])
            elif isinstance(self.params[name], Thunk):
                self.known_params[name] = self.params[name].work()
            else:
                self.known_params[name] = self.params[name]
        return self.known_params[name]

    def variable(self, name):
        if name not in self.known_variables:
            variables = self.template["variables"]
            copy = next((c for c in variables.get("copy", []) if c["name"] == name), None)
            if copy:
                items = []
                for index in range(self.resolve(copy["count"])):
                    self.copies[name] = index
                    items.append(self.resolve(copy["input"]))
                self.known_variables[name] = items
            else:
                self.known_variables[name] = self.resolve(variables[name])
        return self.known_variables[name]

    def resolve(self, value):
        if isinstance(value, str):
            if value.startswith("[") and value.endswith("]") and not value.startswith("[["):
                node, _ = parse_expression(tokenize(value[1:-1]), 0)
                return self.evaluate(node)
            return value
        if isinstance(value, dict):
            return {key: self.resolve(item) for key, item in value.items()}
        if isinstance(value, list):
            return [self.resolve(item) for item in value]
        return value

    def evaluate(self, node):
        if node[0] == "lit":
            return node[1]
        if node[0] == "prop":
            return self.evaluate(node[1])[self.evaluate(node[2])]
        _, name, args = node
        if name == "lambda":
            names, body = [a[1] for a in args[:-1]], args[-1]
            def call(*items):
                saved = {n: self.lambdas.get(n) for n in names}
                self.lambdas.update(zip(names, items))
                try:
                    return self.evaluate(body)
                finally:
                    self.lambdas.update(saved)
            return call
        values = [self.evaluate(a) for a in args]
        if name == "parameters": return self.param(values[0])
        if name == "variables": return self.variable(values[0])
        if name == "lambdaVariables": return self.lambdas[values[0]]
        if name == "copyIndex": return self.copies[values[0]]
        if name == "format": return values[0].format(*[str(v) for v in values[1:]])
        if name == "string": return str(values[0])
        if name == "concat": return [x for v in values for x in v] if isinstance(values[0], list) else "".join(str(v) for v in values)
        if name == "createObject": return dict(zip(values[0::2], values[1::2]))
        if name == "createArray": return list(values)
        if name == "json": return json.loads(values[0])
        if name == "length": return len(values[0])
        if name == "map": return [values[1](x) for x in values[0]]
        if name == "filter": return [x for x in values[0] if values[1](x)]
        if name == "equals": return values[0] == values[1]
        if name == "or": return any(values)
        if name == "and": return all(values)
        if name == "not": return not values[0]
        if name == "if": return values[1] if values[0] else values[2]
        if "." in name:  # a user-defined function, e.g. __bicep.healthProbe
            namespace, member = name.split(".")
            for functions in self.template.get("functions", []):
                if functions["namespace"] == namespace:
                    definition = functions["members"][member]
                    scope = Scope(self.template, {p["name"]: v for p, v in zip(definition["parameters"], values)})
                    return scope.resolve(definition["output"]["value"])
        raise ValueError(f"this check can't work out {name}() in the template")

def find_group(scope):
    """The scope of the template that declares the container group, and the group itself."""
    resources = scope.template.get("resources", [])
    # languageVersion 2.0 templates name their resources: an object instead of a list.
    for resource in resources.values() if isinstance(resources, dict) else resources:
        if resource.get("type") == "Microsoft.ContainerInstance/containerGroups":
            return scope, resource
        nested = resource.get("properties", {}).get("template")
        if nested:
            given = {name: Thunk(lambda expression=parameter["value"]: scope.resolve(expression))
                     for name, parameter in resource["properties"].get("parameters", {}).items()}
            found = find_group(Scope(nested, given))
            if found:
                return found
    return None

def bicepparam(path):
    return {name: p.get("value") for name, p in json.loads(json.load(open(path))["parametersJson"])["parameters"].items()}

def group_for(params_path):
    found = find_group(Scope(main, bicepparam(params_path)))
    if not found:
        print("FAIL: no container group in main.bicep", file=sys.stderr)
        sys.exit(1)
    return found

scope, group = group_for(params_with_path)
properties = group["properties"]
template, variables = scope.template, scope.template.get("variables", {})
# The containers Azure creates for a set of values, by name.
def containers_for(params_path):
    values_scope, values_group = group_for(params_path)
    return {c["name"]: c["properties"] for c in values_scope.resolve(values_group["properties"]["containers"])}
# With both optional versions: every container the group can hold.
containers = {c["name"]: c["properties"] for c in scope.resolve(properties["containers"])}

# Resolve the simple expressions the volumes use: [variables('x')], [json('0.25')], numbers.
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

def environment(container_name):
    return {v["name"]: v for v in containers.get(container_name, {}).get("environmentVariables", [])}

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
# ACI accepts memory only in 0.1 GB steps and refuses the deploy otherwise (MemoryRequirementNotTimesOfOneTenthGB, #179)
off_step = []
for name, container in containers.items():
    gb = float(resolve(container["resources"])["requests"]["memoryInGB"])
    if abs(gb * 10 - round(gb * 10)) > 1e-6:
        off_step.append(f"{name} ({gb:g} GB)")
if off_step:
    fail(f"memory not in 0.1 GB steps: {', '.join(off_step)}")
else:
    ok("every memory request is in 0.1 GB steps")

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

# 7. Every service reads its secret from Vault in the group (#17): the address on the group's localhost,
#    the token as a secureValue. Compose gives it the same settings (deploy/compose/services.yml).
infrastructure = {"caddy", "nginx", "member-portal", "mongodb", "rabbitmq", "vault"}
for name, container in containers.items():
    if name in infrastructure:
        continue
    variables_of = {v["name"]: v for v in container.get("environmentVariables", [])}
    address = str(variables_of.get("Vault__Address", {}).get("value", ""))
    if "127.0.0.1" in address and "secureValue" in variables_of.get("Vault__Token", {}):
        ok(f"{name} reads its secret from Vault on the group's localhost")
    else:
        fail(f"{name} has no Vault__Address on 127.0.0.1 and Vault__Token as a secureValue")

# 8. The member portal only with a UI version (and notification-svc likewise, check 10): the group is worked out
#    with each combination of the two versions, and main.bicepparam sets includeMemberPortal from UI_IMAGE_TAG.
if "member-portal" in containers:
    ok("container member-portal (when it has a release)")
else:
    fail("no container member-portal in the group, with a UI version")
wrong = []
for params_path, with_portal, with_notification, versions in [
        (params_without_path, False, False, "no optional version"),
        (params_ui_path, True, False, "only a UI version"),
        (params_notification_path, False, True, "only a NotificationService version"),
        (params_with_path, True, True, "both versions")]:
    group_containers = containers_for(params_path)
    for name, wanted in [("member-portal", with_portal), ("notification-svc", with_notification)]:
        if (name in group_containers) != wanted:
            wrong.append(f"with {versions} the group {'has no' if wanted else 'has'} {name}")
if wrong:
    fail("; ".join(wrong))
else:
    ok("member-portal and notification-svc are in the group only with their own version")
without_ui, with_ui = bicepparam(params_without_path), bicepparam(params_with_path)
if without_ui.get("includeMemberPortal") is False:
    ok("main.bicepparam without UI_IMAGE_TAG: no member portal")
else:
    fail(f"main.bicepparam without UI_IMAGE_TAG sets includeMemberPortal to {without_ui.get('includeMemberPortal')}, expected false")
if with_ui.get("includeMemberPortal") is True and with_ui.get("uiImageTag") == "1.2.3":
    ok("main.bicepparam with UI_IMAGE_TAG=1.2.3: the member portal 1.2.3")
else:
    fail(f"main.bicepparam with UI_IMAGE_TAG=1.2.3 sets includeMemberPortal {with_ui.get('includeMemberPortal')} "
         f"and uiImageTag {with_ui.get('uiImageTag')}, expected true and 1.2.3")

# The settings of a service's environment block in deploy/compose/services.yml (checks 9 and 12).
def compose_environment(service):
    in_service = in_environment = False
    settings = {}
    with open(os.path.join(root, "deploy/compose/services.yml"), encoding="utf-8") as compose:
        for line in compose.read().splitlines():
            if re.fullmatch(r"  [\w-]+:", line):
                in_service, in_environment = line.strip() == f"{service}:", False
            elif in_service and re.fullmatch(r"    environment:", line):
                in_environment = True
            elif in_service and re.fullmatch(r"    \S.*", line):
                in_environment = False
            elif in_service and in_environment:
                match = re.fullmatch(r"      (\w+): (.*)", line)
                if match:
                    settings[match.group(1)] = re.sub(r"\s+#.*$", "", match.group(2)).strip().strip('"')
    return settings

# 9. NotificationService. The setting names come from its compose block, so a new one there fails here.
compose_settings = [name for name in compose_environment("notification-svc") if "__" in name]
notification = containers.get("notification-svc")
if notification is None:
    fail("no container notification-svc in the group")
else:
    ok("container notification-svc (when it has a release)")
    settings = {v["name"]: v for v in notification.get("environmentVariables", [])}
    missing = [name for name in compose_settings if name not in settings]
    if compose_settings and not missing:
        ok(f"notification-svc has the {len(compose_settings)} settings of its compose block")
    else:
        fail(f"notification-svc lacks compose settings: {missing or 'none found in deploy/compose/services.yml'}")
    # In the group everything is on localhost.
    notification_port = scope.variable("servicePorts")["notification"]
    for name, wanted in [("RabbitMq__HostName", "127.0.0.1"), ("RabbitMq__Port", str(scope.variable("rabbitmqPort"))),
                         ("MongoDb__Host", "127.0.0.1"), ("MongoDb__Port", str(scope.variable("mongoPort"))),
                         ("Loki__Url", "http://127.0.0.1:3100"),
                         ("ASPNETCORE_HTTP_PORTS", str(notification_port))]:
        value = settings.get(name, {}).get("value", "")
        if value == wanted:
            ok(f"notification-svc: {name}")
        else:
            fail(f"notification-svc: {name} is '{value}', expected {wanted}")
    for probe in ("livenessProbe", "readinessProbe"):
        http_get = (notification.get(probe) or {}).get("httpGet", {})
        if http_get.get("path") == "/health" and http_get.get("port") == notification_port:
            ok(f"notification-svc: {probe} is /health on its port")
        else:
            fail(f"notification-svc: {probe} should be /health on its port, got {http_get}")
    image = f"{bicepparam(params_with_path).get('notificationImage')}:4.5.6"
    if notification.get("image") == image:
        ok("notification-svc runs the image in notificationImage")
    else:
        fail(f"notification-svc runs {notification.get('image')}, expected {image}")
if main.get("parameters", {}).get("notificationDbPassword", {}).get("type") == "securestring":
    ok("notificationDbPassword is a secure parameter")
else:
    fail("main.bicep has no secure parameter notificationDbPassword")
for name in ["mongodb", "vault"]:
    settings = {v["name"]: v for v in containers.get(name, {}).get("environmentVariables", [])}
    if settings.get("NOTIFICATION_DB_PASSWORD", {}).get("secureValue") == "made-up-notification-db":
        ok(f"{name} gets NOTIFICATION_DB_PASSWORD from notificationDbPassword")
    else:
        fail(f"{name} doesn't get NOTIFICATION_DB_PASSWORD from notificationDbPassword as a secureValue")

# 10. NotificationService only with a version (check 8 compares the filter).
if without_ui.get("includeNotificationService") is False:
    ok("main.bicepparam without NOTIFICATION_IMAGE_TAG: no notification-svc")
else:
    fail("main.bicepparam without NOTIFICATION_IMAGE_TAG sets includeNotificationService to "
         f"{without_ui.get('includeNotificationService')}, expected false")
if with_ui.get("includeNotificationService") is True and with_ui.get("notificationImageTag") == "4.5.6":
    ok("main.bicepparam with NOTIFICATION_IMAGE_TAG=4.5.6: NotificationService 4.5.6")
else:
    fail("main.bicepparam with NOTIFICATION_IMAGE_TAG=4.5.6 sets includeNotificationService "
         f"{with_ui.get('includeNotificationService')} and notificationImageTag "
         f"{with_ui.get('notificationImageTag')}, expected true and 4.5.6")

# 11. The services (#371): one container each, on the port Nginx routes to, with both probes on /health.
SERVICES = ["booking", "membership", "identity"]
nginx = {name: variable.get("value") for name, variable in environment("nginx").items()}
service_port = {}
for context in SERVICES:
    name = f"{context}-svc"
    container = containers.get(name)
    if container is None:
        fail(f"no container {name} in the group")
        continue
    upstream = str(nginx.get(f"{context.upper()}_URL", ""))
    match = re.fullmatch(r"http://127\.0\.0\.1:(\d+)", upstream)
    port = int(match.group(1)) if match else None
    problems = []
    if port is None:
        problems.append(f"Nginx's {context.upper()}_URL is '{upstream}', not http://127.0.0.1:<port>")
    elif port in service_port.values():
        problems.append(f"port {port} is also another service's")
    elif [p.get("port") for p in container.get("ports", [])] != [port]:
        problems.append(f"it listens on {[p.get('port') for p in container.get('ports', [])]}, not on {port} where Nginx routes to it")
    if port is not None and environment(name).get("ASPNETCORE_HTTP_PORTS", {}).get("value") != str(port):
        problems.append(f"ASPNETCORE_HTTP_PORTS is not {port}")
    for probe in ("livenessProbe", "readinessProbe"):
        http_get = (container.get(probe) or {}).get("httpGet", {})
        if http_get.get("path") != "/health" or http_get.get("port") != port:
            problems.append(f"{probe} is not GET /health on {port}")
    if problems:
        fail(f"{name}: " + "; ".join(problems))
    else:
        service_port[context] = port
        ok(f"{name} on port {port}, ASPNETCORE_HTTP_PORTS and both probes on /health")

# 12. Each service gets the settings its block in deploy/compose/services.yml has, with 127.0.0.1 in place of the
#     container name, and secrets as secureValue. Nothing is left out but LEFT_OUT, and nothing is added except
#     ASPNETCORE_HTTP_PORTS.
LEFT_OUT = {
    "Loki__Url": "Loki isn't in the group (#277)",
}
for context in SERVICES:
    name = f"{context}-svc"
    if name not in containers:
        fail(f"{name}: no container to compare with its compose block")
        continue
    azure, wanted_in_group, problems = environment(name), compose_environment(name), []
    for key, value in wanted_in_group.items():
        if key in LEFT_OUT:
            continue
        got = azure.get(key)
        if got is None:
            problems.append(f"compose sets {key}, the group doesn't")
        elif "${" in value:
            if "secureValue" not in got:
                problems.append(f"{key} must be a secureValue")
        else:
            url = re.fullmatch(r"https?://([^:/]+):(\d+)", value)
            if url:
                host, port = url.groups()
                port = service_port.get(host[:-4], port) if host.endswith("-svc") else port
                want = f"http://127.0.0.1:{port}"
            elif key == "MongoDb__Host":
                want = "127.0.0.1"
            else:
                want = value
            if got.get("value") != want:
                problems.append(f"{key} is {got.get('value')!r}, expected {want!r}")
    for key in azure:
        if key not in wanted_in_group and key != "ASPNETCORE_HTTP_PORTS":
            problems.append(f"{key} is not in its compose block")
    if problems:
        fail(f"{name}: " + "; ".join(problems))
    else:
        ok(f"{name} has the settings of its compose block")

# 13. Nginx renders its template with envsubst from its own environment: every ${...} needs a value.
with open(os.path.join(root, "deploy/compose/nginx/default.conf.template"), encoding="utf-8") as template_file:
    template_variables = sorted(set(re.findall(r"\$\{(\w+)\}", template_file.read())))
no_value = [name for name in template_variables if not nginx.get(name)]
if no_value:
    fail(f"the Nginx container has no value for {', '.join(no_value)}")
else:
    ok(f"the Nginx container has a value for all {len(template_variables)} variables of the template")

# 14. The demo accounts' passwords: each from its own secret, and none of the made-up ones of .env.example.
identity = environment("identity-svc")
for setting, secret in [("Seed__MedlemPassword", "made-up-demo-medlem"), ("Seed__TraenerPassword", "made-up-demo-traener"),
                        ("Seed__AdminPassword", "made-up-demo-admin")]:
    if identity.get(setting, {}).get("secureValue") == secret:
        ok(f"identity-svc: {setting} is the secret {secret}")
    else:
        fail(f"identity-svc: {setting} is not a secureValue from its own secret ({secret})")
example_passwords = {}
with open(os.path.join(root, "deploy/compose/.env.example"), encoding="utf-8") as env_example:
    for line in env_example.read().splitlines():
        match = re.fullmatch(r"(DEMO_\w+_PASSWORD)=(.+)", line)
        if match:
            example_passwords[match.group(1)] = match.group(2)
if len(example_passwords) != 3:
    fail(f"expected three DEMO_*_PASSWORD in deploy/compose/.env.example, found {len(example_passwords)}")
leaks = []
for folder, _, file_names in os.walk(os.path.join(root, "deploy/azure")):
    for file_name in file_names:
        text = open(os.path.join(folder, file_name), encoding="utf-8", errors="replace").read()
        leaks += [f"{os.path.relpath(os.path.join(folder, file_name), root)} has the password of {key}"
                  for key, password in example_passwords.items() if password in text]
if leaks:
    for leak in leaks:
        fail(leak)
else:
    ok("no password of .env.example is in deploy/azure")

# 15. Booking's demo class schedule: the seed key is the one Program.cs reads (Seed:Enabled on 27 Nov 2026, #255),
#     written as an environment variable (: becomes __), "true" in Azure, and the same key in compose.
with open(os.path.join(root, "src/BookingService/Program.cs"), encoding="utf-8") as program:
    seed_keys = sorted(set(re.findall(r'GetValue<bool>\("(Seed:\w+)"\)', program.read())))
if len(seed_keys) != 1:
    fail(f"expected one seed key read in src/BookingService/Program.cs, found {seed_keys}")
else:
    seed_setting = seed_keys[0].replace(":", "__")
    booking = environment("booking-svc")
    if booking.get(seed_setting, {}).get("value") == "true":
        ok(f"booking-svc: {seed_setting} is 'true', the key Program.cs reads ({seed_keys[0]})")
    else:
        fail(f"booking-svc: {seed_setting} should be 'true' (the key Program.cs reads), got {booking.get(seed_setting)}")
    if seed_setting in compose_environment("booking-svc"):
        ok(f"compose's booking-svc sets the same key, {seed_setting}")
    else:
        fail(f"compose's booking-svc doesn't set {seed_setting}, the key Program.cs reads")

if failures:
    print(f"\n{len(failures)} check(s) failed", file=sys.stderr)
    sys.exit(1)
print("\nall checks passed")
PY
