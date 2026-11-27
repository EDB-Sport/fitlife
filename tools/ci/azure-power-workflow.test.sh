#!/usr/bin/env bash
# Checks azure-power.yml (#278): a manual start or stop of the Azure container group from GitHub, on the
# same identity and environment as deploy.yml, and that both wait with the one shared script. The workflow
# never runs on a PR, so this static check (with actionlint, tools/ci/static-check.sh) is its proof.
# Usage: tools/ci/azure-power-workflow.test.sh   (from the repo root, or anywhere in the repo; exit 0 = all pass)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2

python3 -I - .github/workflows/azure-power.yml .github/workflows/deploy.yml <<'PY'
import re
import sys
import yaml

power_path, deploy_path = sys.argv[1], sys.argv[2]
failures = []

def check(ok, message):
    print(("PASS  " if ok else "FAIL  ") + message)
    if not ok:
        failures.append(message)

def load(path):
    with open(path, encoding="utf-8") as f:
        workflow = yaml.safe_load(f)
    # PyYAML reads the key `on` as the boolean True
    return workflow, workflow.get("on", workflow.get(True)) or {}

def steps_of(job):
    return job.get("steps") or []

def step_named(steps, prefix):
    for step in steps:
        if str(step.get("name", "")).startswith(prefix):
            return step
    return None

power, power_on = load(power_path)
deploy, _ = load(deploy_path)

check(set(power_on) == {"workflow_dispatch"},
      "azure-power.yml is started only by workflow_dispatch: no push, PR, schedule or other workflow")
action = ((power_on.get("workflow_dispatch") or {}).get("inputs") or {}).get("action") or {}
check(action.get("type") == "choice" and action.get("required") is True,
      "the action input is a required choice")
check(action.get("options") == ["start", "stop"], "the action input accepts exactly start and stop")

jobs = power.get("jobs") or {}
check(len(jobs) == 1, "azure-power.yml has one job")
job = next(iter(jobs.values()), {})
environment = job.get("environment")
environment_name = environment.get("name") if isinstance(environment, dict) else environment
check(environment_name == "production",
      "the job waits on the production environment, so its required reviewers apply")
check(job.get("permissions") == {"contents": "read", "id-token": "write"},
      "the job grants only contents: read (checkout) and id-token: write (OpenID Connect)")

env = power.get("env") or {}
check(env.get("RESOURCE_GROUP") == "rg-fitlife-prod" and env.get("CONTAINER_GROUP") == "fitlife",
      "it acts on rg-fitlife-prod/fitlife")

steps = steps_of(job)
login = step_named(steps, "Log in to Azure") or {}
uses = str(login.get("uses", ""))
check(re.fullmatch(r"azure/login@[0-9a-f]{40}", uses) is not None,
      "it logs in with azure/login pinned to a full commit SHA")
deploy_login = step_named(steps_of(next(iter((deploy.get("jobs") or {}).values()), {})), "Log in to Azure") or {}
check(uses == deploy_login.get("uses"),
      "it uses the same pinned azure/login as deploy.yml: one identity, one version")
check((login.get("with") or {}) == {
          "client-id": "${{ secrets.AZURE_CLIENT_ID }}",
          "tenant-id": "${{ secrets.AZURE_TENANT_ID }}",
          "subscription-id": "${{ secrets.AZURE_SUBSCRIPTION_ID }}"},
      "azure/login gets the existing fitlife-github-deploy identity from the environment secrets")

power_step = step_named(steps, "Start or stop") or {}
run = str(power_step.get("run", ""))
check((power_step.get("env") or {}).get("ACTION") == "${{ inputs.action }}",
      "the start/stop step reads the action input")
check(re.search(r'case "\$ACTION" in\s+start \| stop\)', run) is not None
      and 'az container "$ACTION" --resource-group "$RESOURCE_GROUP" --name "$CONTAINER_GROUP"' in run,
      "it runs az container start or stop on the group, and the case rejects any other action")

wait = step_named(steps, "Wait for HTTPS") or {}
check(str(wait.get("if", "")) == "inputs.action == 'start'",
      "it waits for HTTPS only after a start")
check("tools/ci/wait-for-health.sh" in str(wait.get("run", "")),
      "the wait is the shared tools/ci/wait-for-health.sh, not a copy")

deploy_wait = step_named(steps_of(next(iter((deploy.get("jobs") or {}).values()), {})), "Wait for HTTPS") or {}
deploy_wait_run = str(deploy_wait.get("run", ""))
check("tools/ci/wait-for-health.sh" in deploy_wait_run,
      "deploy.yml waits with the same shared script")
check("curl" not in deploy_wait_run,
      "deploy.yml no longer carries its own copy of the wait loop")

sys.exit(1 if failures else 0)
PY
