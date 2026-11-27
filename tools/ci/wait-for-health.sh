#!/usr/bin/env bash
# Waits for the container group's HTTPS endpoint to answer /health (deploy.yml and azure-power.yml, #278).
# Caddy asks Let's Encrypt for a certificate on the first start, which takes a moment.
# Usage: tools/ci/wait-for-health.sh <public-url>   (exit 0 = /health answered, 1 = not within 5 minutes, 2 = usage)
set -uo pipefail

if [ $# -ne 1 ]; then
  echo "usage: tools/ci/wait-for-health.sh <public-url>" >&2
  exit 2
fi
url=$1

for attempt in $(seq 1 30); do
  if curl --fail --silent --show-error --max-time 10 "$url/health"; then
    exit 0
  fi
  echo "Attempt $attempt: not ready yet"
  sleep 10
done
echo "::error::$url/health did not answer over HTTPS within 5 minutes"
exit 1
