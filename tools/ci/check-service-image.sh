#!/usr/bin/env bash
# Executable acceptance check for a service's Dockerfile (issue #93, docs/CSHARP_SETUP_TODO.md section 8):
# the image builds with the repo root as context, and the container answers /health with 200.
# Usage: tools/ci/check-service-image.sh <Service>   e.g. tools/ci/check-service-image.sh BookingService
set -uo pipefail

service=${1:?Usage: $0 <Service>}
root=$(cd "$(dirname "$0")/../.." && pwd)
image="fitlife-check/$(tr '[:upper:]' '[:lower:]' <<<"$service"):local"
container="fitlife-check-$$"
trap 'docker rm -f "$container" >/dev/null 2>&1' EXIT

if ! docker build -q -f "$root/src/$service/Dockerfile" -t "$image" "$root" >/dev/null; then
  echo "FAIL  docker build -f src/$service/Dockerfile . failed"
  exit 1
fi
echo "PASS  the image builds with the repo root as context"

docker run -d --name "$container" -p 127.0.0.1::8080 "$image" >/dev/null
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
