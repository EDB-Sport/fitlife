#!/usr/bin/env bash
# deploy-check: broken deployment files fail on the PR, not during a deploy.
# Issue #21, docs/CSHARP_SETUP_TODO.md section 10 job 9. CI runs it in .github/workflows/deploy-check.yml.
#
# Run it locally from anywhere in the repo:  tools/ci/deploy-check.sh
# Needs bash, git, jq and Docker with Compose v2. Every other tool (nginx, yq, gcx, az bicep) runs in a
# pinned container, so nothing is installed on your machine. It only reads files and runs
# `docker compose config` and `docker run --rm`: it never starts or stops the compose stack.
#
# Checks:
#   1. `docker compose config -q` on compose.yaml and every group file, with .env.example as the env file
#   2. no image on :latest or without a tag, in the compose files and in deploy/azure (main.bicepparam)
#   3. `nginx -t` on the Nginx template, rendered by the gateway's own pinned nginx image and environment
#   4. gcx lint of the Grafana dashboards and alert rules, when deploy/compose/grafana/ exists (#6)
#   5. `az bicep build` and `az bicep lint` on deploy/azure/main.bicep, when it exists (#10)
# Every check runs, and the script exits 1 at the end if any of them failed.
set -euo pipefail

# Pinned tool versions. Never :latest (docs/CSHARP_SETUP_TODO.md section 7).
YQ_IMAGE=mikefarah/yq:4.54.2
GCX_VERSION=1.7.1 # https://github.com/grafana/gcx/releases, checksums from gcx_<version>_checksums.txt
GCX_SHA256_AMD64=5f1601feac13d850017064a9b556f11983de6def2986ba0bee402a947659c3e9
GCX_SHA256_ARM64=1d8284557e66dd5c6e4d5b37a651afca98bea42729df7b4aa8720464360b2f54
GCX_RUNNER_IMAGE=alpine:3.24.4
AZ_CLI_IMAGE=mcr.microsoft.com/azure-cli:2.92.0
BICEP_VERSION=v0.49.2

ROOT=$(git rev-parse --show-toplevel)
cd "$ROOT"

COMPOSE_DIR=deploy/compose
ENV_FILE=$COMPOSE_DIR/.env.example
GRAFANA_DIR=$COMPOSE_DIR/grafana
AZURE_DIR=deploy/azure
BICEP_FILE=$AZURE_DIR/main.bicep
BICEPPARAM_FILE=$AZURE_DIR/main.bicepparam

failures=0
WORK=$(mktemp -d "${TMPDIR:-/tmp}/deploy-check.XXXXXX")
trap 'rm -rf "$WORK"' EXIT

step() { printf '\n== %s\n' "$*"; }
ok() { printf 'ok: %s\n' "$*"; }
skip() { printf 'skip: %s\n' "$*"; }
fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$*" >&2
  if [[ -n ${GITHUB_ACTIONS:-} ]]; then printf '::error title=deploy-check::%s\n' "$*"; fi
}

# docker compose with only .env.example: no local .env and no variables from your shell, so a local
# run sees exactly what CI sees. Docker's own connection settings are kept.
compose() {
  local keep=(PATH="$PATH" HOME="$HOME")
  local var
  for var in DOCKER_HOST DOCKER_CONTEXT DOCKER_CONFIG DOCKER_CERT_PATH DOCKER_TLS_VERIFY; do
    if [[ -n ${!var:-} ]]; then keep+=("$var=${!var}"); fi
  done
  env -i "${keep[@]}" docker compose --env-file "$ENV_FILE" "$@"
}

shopt -s nullglob
compose_files=("$COMPOSE_DIR"/*.yaml "$COMPOSE_DIR"/*.yml)
shopt -u nullglob

# ---------------------------------------------------------------------------------------------------
step "1. docker compose config -q (env file: $ENV_FILE)"
if [[ ! -f $ENV_FILE ]]; then
  fail "$ENV_FILE is missing"
fi
if [[ ${#compose_files[@]} -eq 0 ]]; then
  fail "no compose files in $COMPOSE_DIR"
fi
valid_files=()
for file in "${compose_files[@]}"; do
  if compose -f "$file" config -q; then
    ok "$file"
    valid_files+=("$file")
  else
    fail "$file: docker compose config failed (see the error above)"
  fi
done

# The resolved config of every valid file, one JSON document per file.
for file in ${valid_files[@]+"${valid_files[@]}"}; do
  compose -f "$file" config --format json
done >"$WORK/compose.jsonl"

# ---------------------------------------------------------------------------------------------------
step "2. Image tags: no :latest and no image without a tag"
# A reference is pinned when it has a tag other than latest, or a digest (@sha256:...).
check_image() { # <where> <image>
  local where=$1 image=$2 name tag=""
  name=${image%%@*}
  if [[ ${name##*/} == *:* ]]; then tag=${name##*:}; fi
  if [[ $tag == latest ]]; then
    fail "$where uses $image: pin a version instead of :latest"
  elif [[ -z $tag && $image != *@* ]]; then
    fail "$where uses $image without a tag, which means :latest: pin a version"
  else
    ok "$where: $image"
  fi
}

# Images as compose resolves them, so ${VAR:-default} and values from .env.example are checked too.
# A service with only `build:` has no image here; hadolint (DL3007) checks its Dockerfile.
while IFS=$'\t' read -r service image; do
  check_image "compose service '$service'" "$image"
done < <(jq -r '.services // {} | to_entries[] | select(.value.image != null) | "\(.key)\t\(.value.image)"' \
  "$WORK/compose.jsonl" | sort -u)

if [[ -d $AZURE_DIR ]]; then
  # Any string literal on latest, in every Bicep file: 'nginx:latest', or a tag parameter set to 'latest'.
  while IFS= read -r hit; do
    fail "$hit: pin a version instead of latest"
  done < <(grep -rnE --include='*.bicep' --include='*.bicepparam' "[:']latest'" "$AZURE_DIR" || true)

  if [[ -f $BICEPPARAM_FILE ]]; then
    # An image parameter (name contains "image", not "tag") must have a tag or a digest.
    param_re="^[[:space:]]*param[[:space:]]+([A-Za-z0-9_]+)[[:space:]]*=[[:space:]]*'([^']*)'"
    lineno=0
    while IFS= read -r line || [[ -n $line ]]; do
      lineno=$((lineno + 1))
      if [[ $line =~ $param_re ]]; then
        param=${BASH_REMATCH[1]} value=${BASH_REMATCH[2]}
        lower=$(printf '%s' "$param" | tr '[:upper:]' '[:lower:]')
        if [[ $lower == *image* && $lower != *tag* ]]; then
          check_image "$BICEPPARAM_FILE:$lineno param $param" "$value"
        elif [[ $lower == *tag* && -z $value ]]; then
          fail "$BICEPPARAM_FILE:$lineno param $param is empty: set a version"
        fi
      fi
    done <"$BICEPPARAM_FILE"
    ok "$BICEPPARAM_FILE checked"
  else
    skip "$BICEPPARAM_FILE does not exist yet (#10)"
  fi
else
  skip "$AZURE_DIR does not exist yet (#10)"
fi

# ---------------------------------------------------------------------------------------------------
step "3. nginx -t on the rendered Nginx template"
# Every compose service on an nginx image with bind-mounted config (the gateway). The container runs the
# image's own entrypoint, which renders /etc/nginx/templates/*.template with envsubst from the service's
# environment exactly as at start-up, and then runs `nginx -t` instead of starting Nginx.
# --network none: the template keeps upstreams in variables, so nothing is resolved at start. A hostname
# written straight into proxy_pass fails here, as it would in the gateway while that service is down.
nginx_services=$(jq -c '.services // {} | to_entries[]
    | select((.value.image // "") | test("^(docker\\.io/)?(library/)?nginx[:@]"))
    | select([.value.volumes[]? | select(.type == "bind")] | length > 0)
    | {name: .key, image: .value.image, environment: (.value.environment // {}),
       binds: [.value.volumes[] | select(.type == "bind")]}' "$WORK/compose.jsonl" | sort -u)
if [[ -z $nginx_services ]]; then
  fail "no nginx service with a mounted template in the compose files"
fi
seen=" "
while IFS= read -r svc; do
  [[ -n $svc ]] || continue
  name=$(jq -r .name <<<"$svc")
  if [[ $seen == *" $name "* ]]; then continue; fi
  seen+="$name "
  image=$(jq -r .image <<<"$svc")

  args=(--rm --network none)
  while IFS= read -r -d '' kv; do args+=(-e "$kv"); done \
    < <(jq -j '.environment | to_entries[] | "\(.key)=\(.value // "")\u0000"' <<<"$svc")
  while IFS= read -r -d '' mount; do args+=(-v "$mount"); done \
    < <(jq -j '.binds[] | "\(.source):\(.target):ro\u0000"' <<<"$svc")

  # On failure, print the rendered config with line numbers, since nginx reports rendered line numbers.
  if docker run "${args[@]}" --entrypoint sh "$image" -c \
    '/docker-entrypoint.sh nginx -t || { status=$?; for f in /etc/nginx/conf.d/*.conf; do echo "--- rendered $f"; cat -n "$f"; done; exit $status; }' \
    >"$WORK/nginx.log" 2>&1; then
    ok "service '$name' ($image): $(grep -E 'test is successful' "$WORK/nginx.log" || echo 'nginx -t passed')"
  else
    cat "$WORK/nginx.log" >&2
    fail "service '$name' ($image): nginx -t failed on the rendered template (see above)"
  fi
done <<<"$nginx_services"

# ---------------------------------------------------------------------------------------------------
step "4. Grafana dashboards and alert rules (gcx $GCX_VERSION)"
# gcx lints Grafana resources in their API form (kind/apiVersion/spec). Provisioning files have another
# shape, so they are wrapped first: a dashboard JSON becomes a Dashboard (v1beta1), and each rule in an
# alerting file (apiVersion: 1, groups:) becomes an AlertRule (v0alpha1). Files already in API form
# are linted as they are. Datasource and other provisioning files are only checked to parse.
if [[ -d $GRAFANA_DIR ]]; then
  res="$WORK/grafana"
  mkdir -p "$res"
  count=0

  while IFS= read -r -d '' file; do
    rel=${file#"$GRAFANA_DIR"/}
    out="$res/$(printf '%s' "$rel" | tr '/' '_')"
    if ! jq -e . "$file" >/dev/null 2>&1; then
      fail "$file is not valid JSON"
    elif jq -e 'has("kind") and has("apiVersion")' "$file" >/dev/null; then
      cp "$file" "$out" && count=$((count + 1))
    elif jq -e 'has("panels") or has("schemaVersion")' "$file" >/dev/null; then
      jq --arg name "$rel" '{kind: "Dashboard", apiVersion: "dashboard.grafana.app/v1beta1",
          metadata: {name: (.uid // $name)}, spec: .}' "$file" >"$out" && count=$((count + 1))
    else
      skip "$file is neither a dashboard nor a Grafana resource"
    fi
  done < <(find "$GRAFANA_DIR" -type f -name '*.json' -print0 | sort -z)

  while IFS= read -r -d '' file; do
    rel=${file#"$GRAFANA_DIR"/}
    # yq turns every YAML document into one line of JSON (and fails on invalid YAML).
    if ! docker run --rm --network none -v "$ROOT/$GRAFANA_DIR:/workdir:ro" "$YQ_IMAGE" \
      -o=json -I=0 '.' "$rel" >"$WORK/doc.jsonl" 2>"$WORK/yq.err"; then
      cat "$WORK/yq.err" >&2
      fail "$file is not valid YAML"
      continue
    fi
    doc=0
    while IFS= read -r json; do
      doc=$((doc + 1))
      base="$res/$(printf '%s' "$rel" | tr '/' '_').$doc"
      if jq -e 'type == "object" and has("kind") and has("apiVersion")' <<<"$json" >/dev/null; then
        printf '%s\n' "$json" >"$base.json" && count=$((count + 1))
      elif jq -e 'type == "object" and has("groups")' <<<"$json" >/dev/null; then
        n=0
        while IFS= read -r rule; do
          n=$((n + 1))
          printf '%s\n' "$rule" >"$base.rule$n.json" && count=$((count + 1))
        done < <(jq -c --arg file "$rel" '.groups[]? as $g | $g.rules[]? |
            {kind: "AlertRule", apiVersion: "rules.alerting.grafana.app/v0alpha1",
             metadata: {name: (.uid // .title // $file), annotations: {"grafana.app/folder": ($g.folder // "")}},
             spec: .}' <<<"$json")
      fi
    done <"$WORK/doc.jsonl"
  done < <(find "$GRAFANA_DIR" -type f \( -name '*.yml' -o -name '*.yaml' \) -print0 | sort -z)

  if [[ $count -eq 0 ]]; then
    skip "no dashboards or alert rules in $GRAFANA_DIR yet"
  else
    case $(docker version --format '{{.Server.Arch}}') in
      arm64 | aarch64) gcx_arch=arm64 gcx_sha=$GCX_SHA256_ARM64 ;;
      *) gcx_arch=amd64 gcx_sha=$GCX_SHA256_AMD64 ;;
    esac
    gcx_url="https://github.com/grafana/gcx/releases/download/v$GCX_VERSION/gcx_${GCX_VERSION}_linux_$gcx_arch.tar.gz"
    echo "linting $count resource(s); file names are <path under grafana/ with / as _>"
    # The download is checked against the pinned checksum before gcx runs.
    if docker run --rm -v "$res:/resources:ro" -e GCX_URL="$gcx_url" -e GCX_SHA="$gcx_sha" \
      "$GCX_RUNNER_IMAGE" sh -euc '
        cd /tmp
        wget -q -O gcx.tar.gz "$GCX_URL"
        echo "$GCX_SHA  gcx.tar.gz" | sha256sum -c -s -
        tar -xzf gcx.tar.gz gcx
        ./gcx dev lint run /resources'; then
      ok "gcx found no problems in $count resource(s)"
    else
      fail "gcx lint found problems in $GRAFANA_DIR (see above)"
    fi
  fi
else
  skip "$GRAFANA_DIR does not exist yet (#6)"
fi

# ---------------------------------------------------------------------------------------------------
step "5. az bicep build and az bicep lint (Bicep $BICEP_VERSION)"
if [[ -f $BICEP_FILE ]]; then
  # deploy/ is mounted read-only, because main.bicep loads ../compose/nginx/default.conf.template.
  # build writes to stdout only, so no main.json lands in the repo. lint fails on rules set to error
  # in bicepconfig.json.
  if docker run --rm -v "$ROOT/deploy:/src/deploy:ro" -w /src/$AZURE_DIR \
    -e AZURE_CORE_COLLECT_TELEMETRY=false -e AZURE_BICEP_CHECK_VERSION=false \
    -e AZURE_BICEP_USE_BINARY_FROM_PATH=false -e BICEP_VERSION="$BICEP_VERSION" \
    "$AZ_CLI_IMAGE" bash -euc '
      az bicep install --version "$BICEP_VERSION" >/dev/null
      az bicep version
      az bicep build --file main.bicep --stdout >/dev/null
      echo "build ok"
      az bicep lint --file main.bicep'; then
    ok "$BICEP_FILE builds and passes the linter"
  else
    fail "$BICEP_FILE: az bicep build or lint failed (see above)"
  fi
else
  skip "$BICEP_FILE does not exist yet (#10)"
fi

# ---------------------------------------------------------------------------------------------------
echo
if [[ $failures -gt 0 ]]; then
  echo "deploy-check: $failures problem(s) found" >&2
  exit 1
fi
echo "deploy-check: all checks passed"
