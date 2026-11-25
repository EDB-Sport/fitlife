#!/usr/bin/env bash
# Checks the `images` job in .github/workflows/release.yml for the rules of issue #282: each image is
# built once, and the image pushed to Docker Hub is the one Trivy scanned (METHODOLOGY section 10).
# Usage: tools/ci/release-single-build.test.sh   (from anywhere in the repo; exit 0 = all pass)
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2
file=.github/workflows/release.yml

if [ ! -f "$file" ]; then
  echo "FAIL  $file does not exist"
  exit 1
fi

python3 -I - "$file" <<'PY'
import sys
import yaml

with open(sys.argv[1], encoding="utf-8") as f:
    workflow = yaml.safe_load(f)

failures = []

def check(ok, message):
    print(("PASS  " if ok else "FAIL  ") + message)
    if not ok:
        failures.append(message)

def uses(step, action):
    return str(step.get("uses", "")).startswith(action + "@")

jobs = workflow.get("jobs", {})
steps = jobs.get("images", {}).get("steps", [])

def index_of(predicate):
    return next((i for i, step in enumerate(steps) if predicate(step)), None)

builds = [s for s in steps if uses(s, "docker/build-push-action")]
check(len(builds) == 1, f"build-push-action is used once in the images job (found {len(builds)})")
check(all(not (s.get("with") or {}).get("push") for s in builds),
      "no build-push-action step pushes: the push reuses the scanned image")

build = index_of(lambda s: uses(s, "docker/build-push-action"))
check(build is not None and (steps[build].get("with") or {}).get("load") is True,
      "the build loads the image so Trivy scans the image that will be pushed")

scan = index_of(lambda s: uses(s, "aquasecurity/trivy-action"))
login = index_of(lambda s: uses(s, "docker/login-action"))
push = index_of(lambda s: "docker push" in str(s.get("run", "")))
check(push is not None, "a step runs docker push on the loaded tags")

if None not in (build, scan, login, push):
    check(build < scan < login < push, "order is build, Trivy scan, login, push")

tags = jobs.get("tags", {})
needs = tags.get("needs") or []
needs = [needs] if isinstance(needs, str) else needs
check("images" in needs, "the tags job still needs the images job")

sys.exit(1 if failures else 0)
PY
