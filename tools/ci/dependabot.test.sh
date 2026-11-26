#!/usr/bin/env bash
# Checks .github/dependabot.yml for the rules of issue #281: three ecosystems, monthly, one group each,
# the .NET SDK image ignored, and a low open-pull-requests-limit (METHODOLOGY section 10).
# Usage: tools/ci/dependabot.test.sh   (from the repo root, or anywhere in the repo; exit 0 = all pass)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" || exit 2
file=.github/dependabot.yml

if [ ! -f "$file" ]; then
  echo "FAIL  $file does not exist"
  exit 1
fi

# Without PyYAML: skip on a laptop, fail in CI (CI=true on GitHub Actions), so ci-scripts never passes untested (#449)
if ! python3 -I -c 'import yaml' 2>/dev/null; then
  if [ "${CI:-}" = true ]; then
    echo "FAIL  PyYAML is not installed, so this test can't run in CI (pip install pyyaml)"
    exit 1
  fi
  echo "SKIP: PyYAML not installed (pip install pyyaml)"
  exit 0
fi

python3 -I - "$file" <<'PY'
import sys
import yaml

path = sys.argv[1]
failures = []

def check(ok, message):
    print(("PASS  " if ok else "FAIL  ") + message)
    if not ok:
        failures.append(message)

with open(path, encoding="utf-8") as f:
    config = yaml.safe_load(f)

check(config.get("version") == 2, "version is 2")
updates = config.get("updates") or []
by_ecosystem = {}
for update in updates:
    by_ecosystem.setdefault(update.get("package-ecosystem"), []).append(update)

check(sorted(by_ecosystem) == ["docker", "docker-compose", "github-actions"],
      "exactly three ecosystems: github-actions, docker, docker-compose (owner decision on #281)")

for ecosystem in ["github-actions", "docker", "docker-compose"]:
    entries = by_ecosystem.get(ecosystem, [])
    check(len(entries) == 1, f"{ecosystem} is configured once")
    if len(entries) != 1:
        continue
    entry = entries[0]
    check((entry.get("schedule") or {}).get("interval") == "monthly",
          f"{ecosystem} checks monthly")
    groups = entry.get("groups") or {}
    check(len(groups) == 1 and any(g.get("patterns") == ["*"] for g in groups.values()),
          f"{ecosystem} has one group that takes every update")
    limit = entry.get("open-pull-requests-limit")
    check(isinstance(limit, int) and 1 <= limit <= 2,
          f"{ecosystem} open-pull-requests-limit is 1 or 2 (METHODOLOGY section 10)")

docker = (by_ecosystem.get("docker") or [{}])[0]
compose = (by_ecosystem.get("docker-compose") or [{}])[0]
check("/src/*" in (docker.get("directories") or []), "docker covers /src/* (the Dockerfiles)")
check("/deploy/compose" == compose.get("directory"), "docker-compose covers /deploy/compose")

# Dependabot names an MCR image without the registry host ("dotnet/sdk", PR #287), so the ignore must too (#295).
ignores = {i.get("dependency-name"): i for i in (docker.get("ignore") or [])}
sdk = ignores.get("dotnet/sdk")
check(sdk is not None and "update-types" not in sdk and "versions" not in sdk,
      "docker ignores every update of the .NET SDK image, named dotnet/sdk (ADR 004: global.json moves with it)")
aspnet = ignores.get("dotnet/aspnet") or {}
check(aspnet.get("update-types") == ["version-update:semver-major"],
      "docker ignores major updates of dotnet/aspnet, so the runtime stays on the app's .NET major (#295)")

with open(path, encoding="utf-8") as f:
    text = f.read()
check("nuget" not in [u.get("package-ecosystem") for u in updates],
      "NuGet is left out")
check("NuGet" in text and "packages.lock.json" in text,
      "the file says why NuGet is left out (packages.lock.json)")
check("SDK" in text and "global.json" in text and "#" in text,
      "the file says why the SDK image is ignored, in a comment")

sys.exit(1 if failures else 0)
PY
