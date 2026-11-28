#!/usr/bin/env bash
# Checks the "stack" launch profiles (issue #525): every service built in deploy/compose/services.yml has a profile
# named stack in src/<project>/Properties/launchSettings.json, on its own port, and the README section
# "Run one service from your IDE" lists it with that port and the gateway variable that points at it.
# Usage: tools/ci/launch-profiles.test.sh   (exit code 0 = every case passes; needs jq)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
services="$root/deploy/compose/services.yml"
readme="$root/deploy/compose/README.md"
failures=0

check() { # check <description> <command...>
  if "${@:2}"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1"
    echo "      ran: ${*:2}"
    failures=$((failures + 1))
  fi
}

# The README section, from its heading to the next "## " heading
section=$(awk '/^## Run one service from your IDE$/ { inside = 1; print; next } /^## / { inside = 0 } inside' "$readme")
check "deploy/compose/README.md has the section \"Run one service from your IDE\"" test -n "$section"

# <compose service> <project> pairs: each service in services.yml that is built from src/<project>/Dockerfile
pairs=$(awk '
  /^  [a-z0-9-]+:$/ { name = $1; sub(/:$/, "", name) }
  /dockerfile: src\/[^\/]+\/Dockerfile/ { project = $2; sub(/^src\//, "", project); sub(/\/Dockerfile$/, "", project); print name, project }
' "$services")
check "services.yml builds at least one service from src/" test -n "$pairs"

ports=""
while read -r service project; do
  settings="$root/src/$project/Properties/launchSettings.json"
  url=$(jq -r '.profiles.stack.applicationUrl // empty' "$settings" 2>/dev/null)
  check "$project has a launch profile named stack" test -n "$url"
  port=${url##*:}
  ports="$ports $port"
  check "$project's stack profile runs on its own port, not the gateway's 8080" test "${port:-8080}" != 8080
  check "the README section lists $service on port $port" grep -qE "^\| \`$service\` \|.*\| ${port:-none} \|" <<<"$section"
done <<<"$pairs"

duplicates=$(tr ' ' '\n' <<<"$ports" | grep -v '^$' | sort | uniq -d)
check "no two stack profiles share a port" test -z "$duplicates"

exit "$failures"
