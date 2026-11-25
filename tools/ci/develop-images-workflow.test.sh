#!/usr/bin/env bash
# Checks .github/workflows/develop-images.yml for the rules of issue #340: it runs on push to develop only, builds each
# changed service once, scans that image with Trivy as release.yml does, and pushes only :develop and :sha-<short>:
# never latest, never an X.Y.Z tag, and no secret but the Docker Hub credentials the release uses.
# Usage: tools/ci/develop-images-workflow.test.sh   (from anywhere in the repo; exit 0 = all pass)
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2
file=.github/workflows/develop-images.yml

if [ ! -f "$file" ]; then
  echo "FAIL  $file does not exist"
  exit 1
fi

python3 -I - "$file" .github/workflows/release.yml <<'PY'
import re
import sys
import yaml

with open(sys.argv[1], encoding="utf-8") as f:
    text = f.read()
workflow = yaml.safe_load(text)
with open(sys.argv[2], encoding="utf-8") as f:
    release = yaml.safe_load(f)

failures = []

def check(ok, message):
    print(("PASS  " if ok else "FAIL  ") + message)
    if not ok:
        failures.append(message)

def uses(step, action):
    return str(step.get("uses", "")).startswith(action + "@")

triggers = workflow.get(True, workflow.get("on", {}))  # YAML reads a bare `on` as True
check(list(triggers) == ["push"], f"the only trigger is push (found {list(triggers)})")
check((triggers.get("push") or {}).get("branches") == ["develop"], "it runs on push to develop only")

jobs = workflow.get("jobs", {})
check("plan" in jobs and "images" in jobs, "there is a plan job and an images job")
plan_steps = jobs.get("plan", {}).get("steps", [])
check(any("tools/ci/develop-plan.sh" in str(s.get("run", "")) for s in plan_steps), "plan runs tools/ci/develop-plan.sh")
check("${{ github.event.before }}" in text, "the plan compares with the commit before the push")

steps = jobs.get("images", {}).get("steps", [])
def index_of(predicate):
    return next((i for i, step in enumerate(steps) if predicate(step)), None)

builds = [s for s in steps if uses(s, "docker/build-push-action")]
check(len(builds) == 1, f"build-push-action is used once (found {len(builds)})")
check(all(not (s.get("with") or {}).get("push") for s in builds), "no build-push-action step pushes: the push reuses the scanned image")
build = index_of(lambda s: uses(s, "docker/build-push-action"))
check(build is not None and (steps[build].get("with") or {}).get("load") is True, "the build loads the image so Trivy scans what is pushed")
scan = index_of(lambda s: uses(s, "aquasecurity/trivy-action"))
login = index_of(lambda s: uses(s, "docker/login-action"))
push = index_of(lambda s: "docker push" in str(s.get("run", "")))
check(None not in (build, scan, login, push) and build < scan < login < push, "order is build, Trivy scan, login, push")

def trivy(workflow_jobs):
    for job in workflow_jobs.values():
        for s in job.get("steps", []) or []:
            if uses(s, "aquasecurity/trivy-action"):
                w = dict(s.get("with") or {})
                w.pop("image-ref", None)
                return w
trivy_here = trivy(jobs)
check(trivy_here is not None and trivy_here == trivy(release.get("jobs", {})), "Trivy has the settings of release.yml")

meta = next((s for s in steps if uses(s, "docker/metadata-action")), None)
tags = str((meta or {}).get("with", {}).get("tags", ""))
check("type=raw,value=develop" in tags and "type=sha" in tags, "the tags are develop and sha-<short>")
check("latest" not in tags and "semver" not in tags, "no latest tag and no X.Y.Z tag")
check(not re.search(r"(?<!ubuntu-)\blatest\b|semver", re.sub(r"(?m)#.*$", "", text)), "the workflow never mentions latest or semver outside comments")
secrets = set(re.findall(r"secrets\.([A-Z_]+)", text))
check(secrets <= {"DOCKERHUB_USERNAME", "DOCKERHUB_TOKEN"}, f"the only secrets are the Docker Hub credentials (found {sorted(secrets)})")
check(workflow.get("permissions") == {"contents": "read"}, "permissions are contents: read")

sys.exit(1 if failures else 0)
PY
