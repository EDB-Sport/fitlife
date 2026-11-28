#!/usr/bin/env bash
# The ci.yml jobs that restore the whole solution take the NuGet packages from the Actions cache (issue #601):
# their actions/setup-dotnet step has cache: true, keyed on every tracked packages.lock.json.
# Every other setup-dotnet step has no cache, because:
#   - its post step fails the job when the job never restored into ~/.nuget/packages ("Cache folder path is
#     retrieved for .NET CLI but doesn't exist on disk"): ci-scripts, and tdd-check on a PR without C# changes
#   - a job that restores only some packages (throwaway projects, changed projects) could save the cache first,
#     and every job after it would get that partial cache under the same key: build-rules, mutation, tdd-check
# Usage: tools/ci/setup-dotnet-cache.test.sh   (exit 0 = every check passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0
cached_jobs=" ci.yml:restore ci.yml:build ci.yml:format ci.yml:test ci.yml:contract "

# setup_dotnet_steps <workflow file>: one line per setup-dotnet step, "<job>|<line number>|<its with: keys, joined by ;>"
setup_dotnet_steps() {
  awk '
    function flush() { if (start) print job "|" start "|" keys; start = 0; keys = "" }
    /^jobs:/ { in_jobs = 1; next }
    in_jobs && /^  [A-Za-z0-9_-]+:/ { flush(); job = $1; sub(/:$/, "", job); next }
    /uses: actions\/setup-dotnet@/ {
      flush(); start = NR; match($0, /^ */); step_indent = RLENGTH; next
    }
    start {
      match($0, /^ */)
      if ($0 ~ /^ *- / && RLENGTH <= step_indent) { flush(); next }   # the next step
      line = $0; sub(/^ +/, "", line); sub(/ +#.*$/, "", line)
      if (line ~ /^[a-z-]+: /) keys = keys line ";"
    }
    END { flush() }
  ' "$1"
}

# has_key <keys> <key>
has_key() { case ";$1" in *";$2;"*) return 0 ;; *) return 1 ;; esac; }

# expect <description> <command...>
expect() {
  local description=$1
  shift
  if "$@"; then
    echo "PASS  $description"
  else
    echo "FAIL  $description"
    failures=$((failures + 1))
  fi
}

found=" "
for workflow in "$root"/.github/workflows/*.yml; do
  name=$(basename "$workflow")
  while IFS='|' read -r job line keys; do
    [ -n "$line" ] || continue
    expect "$name:$line ($job) sets up the SDK pinned in global.json" has_key "$keys" "global-json-file: global.json"
    case "$cached_jobs" in
      *" $name:$job "*)
        found="$found$name:$job "
        expect "$name:$line ($job) has cache: true" has_key "$keys" "cache: true"
        expect "$name:$line ($job) keys the cache on **/packages.lock.json" \
          has_key "$keys" "cache-dependency-path: '**/packages.lock.json'"
        ;;
      *)
        expect "$name:$line ($job) has no cache (see the header)" bash -c '! grep -qE ";cache: " <<<";$1"' _ "$keys"
        ;;
    esac
  done < <(setup_dotnet_steps "$workflow")
done

# The check reads the steps at all: each cached job is found
for job in $cached_jobs; do
  expect "$job has a setup-dotnet step" grep -qF " $job " <<<"$found"
done
expect "the repo tracks packages.lock.json files for the cache key" \
  test -n "$(git -C "$root" ls-files '*packages.lock.json')"

exit $((failures > 0))
