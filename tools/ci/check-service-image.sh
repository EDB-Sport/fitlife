#!/usr/bin/env bash
# Executable acceptance check for a service's Dockerfile (issue #93, docs/CSHARP_SETUP_TODO.md section 8):
# the image builds with the repo root as context, and the container answers /health with 200.
# Usage: tools/ci/check-service-image.sh <Service> [KEY=VALUE ...]
#   e.g. tools/ci/check-service-image.sh BookingService
#        tools/ci/check-service-image.sh MembershipService Jwt__SigningKey=image-check-key-at-least-32-bytes-long
# Each KEY=VALUE is passed to the container with -e, for a service that needs a setting to start (#223).
# Use made-up test values only: in compose and Azure the real ones come from Vault.
set -uo pipefail

usage="Usage: $0 <Service> [KEY=VALUE ...]"
service=${1:?$usage}
shift
env_args=()
for setting in "$@"; do
  if [[ "$setting" != ?*=* ]]; then
    echo "FAIL  '$setting' is not KEY=VALUE. $usage"
    exit 2
  fi
  env_args+=(-e "$setting")
done
root=$(cd "$(dirname "$0")/../.." && pwd)
image="fitlife-check/$(tr '[:upper:]' '[:lower:]' <<<"$service"):local"
container="fitlife-check-$$"

# The build's output is kept in a file: quiet when it works, the last 40 lines when it fails, so the
# compiler error is in the log (#423). -q would hide it: BuildKit then prints only "failed to solve".
build_log=$(mktemp)
trap 'docker rm -f "$container" >/dev/null 2>&1; rm -f "$build_log"' EXIT
if ! docker build --progress=plain -f "$root/src/$service/Dockerfile" -t "$image" "$root" >"$build_log" 2>&1; then
  echo "FAIL  docker build -f src/$service/Dockerfile . failed. The last 40 lines of its output:"
  tail -40 "$build_log"
  exit 1
fi
echo "PASS  the image builds with the repo root as context"

# ${a[@]+"${a[@]}"}: an empty array under set -u, also in bash 3.2 (macOS)
docker run -d --name "$container" ${env_args[@]+"${env_args[@]}"} -p 127.0.0.1::8080 "$image" >/dev/null
port=$(docker port "$container" 8080/tcp | head -1 | sed 's/.*://')
status=000
for _ in $(seq 1 30); do
  status=$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$port/health" || true)
  [ "$status" = 200 ] && break
  sleep 1
done

if [ "$status" = 200 ]; then
  echo "PASS  the container answers /health with 200 on port 8080"
else
  echo "FAIL  /health answered $status"
  docker logs "$container" 2>&1 | tail -20
  exit 1
fi
