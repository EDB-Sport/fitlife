#!/usr/bin/env bash
# Checks how deploy.yml is started (issue #319). A workflow_run workflow always runs on the default branch
# (develop), so the production environment, which allows only main, rejected every automatic deploy.
# release.yml now calls deploy.yml as a reusable workflow after the tags job, in the push context of main,
# and workflow_dispatch stays for a manual deploy or a rollback.
# Usage: tools/ci/deploy-workflow.test.sh   (from the repo root, or anywhere in the repo; exit 0 = all pass)
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
check((dispatch_inputs.get("service_tag") or {}).get("required") is True,
      "a manual deploy still names the service version")

deploy_job = (deploy.get("jobs") or {}).get("deploy") or {}
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

if failures:
    print(f"{len(failures)} case(s) failed.")
    sys.exit(1)
print("deploy.yml runs after a release, on main.")
PY
