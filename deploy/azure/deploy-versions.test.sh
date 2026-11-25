#!/usr/bin/env bash
# Acceptance check for #198: which image versions .github/workflows/deploy.yml deploys.
# It takes the script of the "Choose image versions" step out of deploy.yml and runs it in a scratch
# repo with release tags, as both triggers would, so it needs no GitHub and no Azure.
#
# Run it from anywhere in the repo:  deploy/azure/deploy-versions.test.sh
# Needs bash, git and awk.
#
# Checks:
#   1. after a release (workflow_run): the newest bookingservice/v* and memberportal/v* tags, and without a
#      memberportal/v* tag no member portal
#   2. a manual run (workflow_dispatch): the versions typed in; an empty ui_tag means the released member
#      portal, so redeploying a service by hand doesn't drop a portal that is live
#   3. a missing service version, latest or a typo fails the run, for the service and the member portal
set -uo pipefail

ROOT=$(git rev-parse --show-toplevel)
WORKFLOW="$ROOT/.github/workflows/deploy.yml"

WORK=$(mktemp -d "${TMPDIR:-/tmp}/deploy-versions-test.XXXXXX")
trap 'rm -rf "$WORK"' EXIT

failures=0
ok() { echo "ok: $1"; }
fail() { echo "FAIL: $*"; failures=$((failures + 1)); }

# The step's run: block, without its indentation.
awk '
  /- name: Choose image versions$/ { step = 1; next }
  step && !inside && /^ *run: \|$/ { inside = 1; indent = index($0, "run:"); next }
  inside && /^ *- name:/ { exit }
  inside && NF && match($0, /^ */) && RLENGTH < indent { exit }
  inside { print substr($0, indent + 2) }
' "$WORKFLOW" >"$WORK/step.sh"
if ! grep -q 'released_version' "$WORK/step.sh"; then
  echo "FAIL: no \"Choose image versions\" step with a run: block in $WORKFLOW"
  exit 1
fi

# run_step <event> <service input> <ui input> <tag or +>...: runs the step in a fresh repo with the tags on
# a commit each ('+' starts the next commit). Sets $status, and $service_tag / $ui_tag / $ui_env from what it wrote.
run_step() {
  local event=$1 input_service=$2 input_ui=$3
  shift 3
  local repo="$WORK/repo-$RANDOM$RANDOM"
  git init -q "$repo"
  git -C "$repo" -c user.name=test -c user.email=test@example.invalid commit -q --allow-empty -m release
  local tag
  for tag in "$@"; do
    if [[ $tag == + ]]; then
      git -C "$repo" -c user.name=test -c user.email=test@example.invalid commit -q --allow-empty -m next
    else
      git -C "$repo" tag "$tag"
    fi
  done
  : >"$repo.env"
  : >"$repo.out"
  (cd "$repo" && GITHUB_EVENT_NAME=$event INPUT_SERVICE_TAG=$input_service INPUT_UI_TAG=$input_ui \
    GITHUB_ENV="$repo.env" GITHUB_OUTPUT="$repo.out" SERVICE_TAG_PREFIX=bookingservice UI_TAG_PREFIX=memberportal \
    bash --noprofile --norc -eo pipefail "$WORK/step.sh" >"$repo.log" 2>&1)
  status=$?
  service_tag=$(sed -n 's/^service_tag=//p' "$repo.out")
  ui_tag=$(sed -n 's/^ui_tag=//p' "$repo.out")
  ui_env=$(grep '^UI_IMAGE_TAG=' "$repo.env" || true)
}

# expect_deploy <description> <service> <ui or empty> <run_step arguments...>
expect_deploy() {
  local description=$1 want_service=$2 want_ui=$3
  shift 3
  run_step "$@"
  local want_env=""
  [[ -n $want_ui ]] && want_env="UI_IMAGE_TAG=$want_ui"
  if [[ $status -eq 0 && $service_tag == "$want_service" && $ui_tag == "$want_ui" && $ui_env == "$want_env" ]]; then
    ok "$description"
  else
    fail "$description: expected service $want_service and member portal '${want_ui}', got exit $status," \
      "service '$service_tag', member portal '$ui_tag', '${ui_env:-no UI_IMAGE_TAG}'"
  fi
}

# expect_refused <description> <run_step arguments...>
expect_refused() {
  local description=$1
  shift
  run_step "$@"
  if [[ $status -ne 0 ]]; then
    ok "$description"
  else
    fail "$description: the step succeeded with service '$service_tag' and member portal '$ui_tag'"
  fi
}

# 1. After a release.
expect_deploy "release without a member portal release: the service, no member portal" 0.1.0 "" \
  workflow_run "" "" bookingservice/v0.1.0
expect_deploy "release with an earlier member portal release: both, the newest tags in the history" 0.2.0 0.3.0 \
  workflow_run "" "" bookingservice/v0.1.0 memberportal/v0.3.0 + bookingservice/v0.2.0
expect_refused "release without a bookingservice tag is refused" \
  workflow_run "" "" memberportal/v0.3.0

# 2. A manual run.
expect_deploy "manual run, empty ui_tag, no member portal release: no member portal" 0.1.0 "" \
  workflow_dispatch 0.1.0 "" bookingservice/v0.1.0
expect_deploy "manual run, empty ui_tag, a member portal release: the released member portal" 0.1.0 0.3.0 \
  workflow_dispatch 0.1.0 "" bookingservice/v0.1.0 memberportal/v0.3.0
expect_deploy "manual run with ui_tag: the version typed in" 0.1.0 0.4.0 \
  workflow_dispatch 0.1.0 0.4.0 bookingservice/v0.1.0 memberportal/v0.3.0

# 3. Released versions only.
expect_refused "manual run with an empty service_tag is refused" workflow_dispatch "" "" bookingservice/v0.1.0
expect_refused "manual run with service_tag latest is refused" workflow_dispatch latest "" bookingservice/v0.1.0
expect_refused "manual run with ui_tag latest is refused" workflow_dispatch 0.1.0 latest bookingservice/v0.1.0
expect_refused "manual run with ui_tag v0.3.0 (a typo for 0.3.0) is refused" \
  workflow_dispatch 0.1.0 v0.3.0 bookingservice/v0.1.0 memberportal/v0.3.0

if [[ $failures -gt 0 ]]; then
  echo
  echo "$failures check(s) failed" >&2
  exit 1
fi
echo
echo "all checks passed"
