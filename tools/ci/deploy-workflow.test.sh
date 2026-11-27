#!/usr/bin/env bash
# Checks how deploy.yml is started (issue #319). A workflow_run workflow always runs on the default branch
# (develop), so the production environment, which allows only main, rejected every automatic deploy.
# release.yml now calls deploy.yml as a reusable workflow after the tags job, in the push context of main,
# and workflow_dispatch stays for a manual deploy or a rollback.
# Usage: tools/ci/deploy-workflow.test.sh   (from the repo root, or anywhere in the repo; exit 0 = all pass)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2

python3 -I - .github/workflows/deploy.yml .github/workflows/release.yml <<'PY'
import sys
import yaml

deploy_path, release_path = sys.argv[1], sys.argv[2]
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

deploy, deploy_on = load(deploy_path)
release, _ = load(release_path)

check("workflow_run" not in deploy_on,
      "deploy.yml isn't started by workflow_run, which runs on develop, not main")
check("workflow_call" in deploy_on, "deploy.yml can be called by release.yml (workflow_call)")
check("workflow_dispatch" in deploy_on, "deploy.yml can still be started by hand: a manual deploy or a rollback")
dispatch_inputs = (deploy_on.get("workflow_dispatch") or {}).get("inputs") or {}
version_inputs = ["booking_tag", "membership_tag", "identity_tag", "ui_tag", "notification_tag"]
check(all(name in dispatch_inputs for name in version_inputs) and "service_tag" not in dispatch_inputs,
      "a manual deploy can still name the version of each image (the rollback): " + ", ".join(version_inputs))
check(all(not (dispatch_inputs.get(name) or {}).get("required") for name in version_inputs),
      "none of them is required: an empty one means the newest release (tools/ci/deploy-versions.sh)")

deploy_job = (deploy.get("jobs") or {}).get("deploy") or {}
steps = {step.get("name"): step for step in deploy_job.get("steps") or []}

# The lookup is a script with its own test (#371), not shell in the workflow.
versions = steps.get("Choose image versions") or {}
check("tools/ci/deploy-versions.sh" in (versions.get("run") or "") and "git describe" not in (versions.get("run") or ""),
      "the version lookup is tools/ci/deploy-versions.sh, not shell in the workflow")
check(all((versions.get("env") or {}).get("INPUT_" + name.upper()) == "${{ inputs." + name + " }}" for name in version_inputs),
      "the script gets the typed-in versions as " + ", ".join("INPUT_" + name.upper() for name in version_inputs))

# main.bicepparam reads the demo passwords from the environment: what-if and deploy both need them.
demo_secrets = ["DEMO_MEDLEM_PASSWORD", "DEMO_TRAENER_PASSWORD", "DEMO_ADMIN_PASSWORD"]
for step_name in ["What-if", "Deploy"]:
    step_env = (steps.get(step_name) or {}).get("env") or {}
    check(all(step_env.get(name) == "${{ secrets." + name + " }}" for name in demo_secrets),
          f"the {step_name} step gets {', '.join(demo_secrets)} from the production secrets")

# The smoke test logs in as the Medlem demo account. The password is written to a globals file at run time:
# on the command line it would show in the runner's process list.
smoke = steps.get("Smoke test (Newman)") or {}
smoke_run = smoke.get("run") or ""
check((smoke.get("env") or {}).get("DEMO_MEDLEM_PASSWORD") == "${{ secrets.DEMO_MEDLEM_PASSWORD }}",
      "the smoke test gets the Medlem password from the secret DEMO_MEDLEM_PASSWORD")
check("--globals" in smoke_run and "medlemPassword=" not in smoke_run and "serviceBasePath" not in smoke_run,
      "the smoke test reads the password from a globals file, not from the command line")
environment = deploy_job.get("environment")
environment_name = environment.get("name") if isinstance(environment, dict) else environment
check(environment_name == "production", "the deploy job still waits for the production approval")
check("workflow_run" not in yaml.safe_dump(deploy_job),
      "the deploy job reads nothing from a workflow_run event")
check("concurrency" in deploy_job, "one deploy at a time, set on the job so it holds when release.yml calls it")

callers = [(name, job) for name, job in (release.get("jobs") or {}).items()
           if job.get("uses") == "./.github/workflows/deploy.yml"]
check(len(callers) == 1, "release.yml calls deploy.yml in exactly one job")
if callers:
    name, job = callers[0]
    needs = job.get("needs") or []
    needs = [needs] if isinstance(needs, str) else needs
    check("tags" in needs, f"release.yml's {name} job runs after the tags job, so a failed release deploys nothing")
    check("if" in job and "needs.plan.outputs.tags" in str(job["if"]),
          f"release.yml's {name} job runs only when this merge released something")
    permissions = job.get("permissions") or {}
    check(permissions.get("id-token") == "write" and permissions.get("contents") == "read",
          f"release.yml's {name} job grants what azure/login needs (id-token: write, contents: read) and no more")
    check(job.get("secrets") == "inherit", f"release.yml's {name} job passes the production secrets on")

sys.exit(1 if failures else 0)
PY
