#!/usr/bin/env bash
# Every actions/setup-dotnet step in .github/workflows/*.yml restores the NuGet packages from the Actions cache
# (issue #601): cache: true, with every tracked packages.lock.json as the key, so a job doesn't download them again.
# Usage: tools/ci/setup-dotnet-cache.test.sh   (exit 0 = every check passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

# setup_dotnet_steps <workflow file>: one line per setup-dotnet step, "<line number>|<its with: keys, joined by ;>"
setup_dotnet_steps() {
  awk '
    function flush() { if (start) print start "|" keys; start = 0; keys = "" }
    /uses: actions\/setup-dotnet@/ {
      flush(); start = NR; match($0, /^ */); step_indent = RLENGTH; next
    }
    start {
      match($0, /^ */)
      if ($0 ~ /^ *- / && RLENGTH <= step_indent) { flush(); next }   # the next step
      if ($0 ~ /^ *[A-Za-z_-]+:/ && RLENGTH < step_indent) { flush(); next } # the next job or key
      line = $0; sub(/^ +/, "", line); sub(/ +#.*$/, "", line)
      if (line ~ /^[a-z-]+: /) keys = keys line ";"
    }
    END { flush() }
  ' "$1"
}

steps=0
for workflow in "$root"/.github/workflows/*.yml; do
  name=$(basename "$workflow")
  while IFS='|' read -r line keys; do
    [ -n "$line" ] || continue
    steps=$((steps + 1))
    for want in "cache: true" "cache-dependency-path: '**/packages.lock.json'" "global-json-file: global.json"; do
      case ";$keys" in
        *";$want;"*) echo "PASS  $name:$line setup-dotnet has $want" ;;
        *)
          echo "FAIL  $name:$line setup-dotnet has $want"
          echo "  its with: keys are: ${keys:-none}"
          failures=$((failures + 1))
          ;;
      esac
    done
  done < <(setup_dotnet_steps "$workflow")
done

# The check reads the steps at all: ci.yml and tdd-check.yml set up .NET (9 steps on develop when #601 was filed)
if [ "$steps" -ge 9 ]; then
  echo "PASS  the workflows have at least 9 setup-dotnet steps (found $steps)"
else
  echo "FAIL  the workflows have at least 9 setup-dotnet steps (found $steps)"
  failures=$((failures + 1))
fi
# The key covers every lock file: there is one at least, and the pattern finds them under the subfolders
locks=$(git -C "$root" ls-files '*packages.lock.json' | wc -l | tr -d ' ')
if [ "$locks" -gt 0 ]; then
  echo "PASS  the repo tracks packages.lock.json files for the cache key ($locks)"
else
  echo "FAIL  the repo tracks packages.lock.json files for the cache key (none)"
  failures=$((failures + 1))
fi

exit $((failures > 0))
