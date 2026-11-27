#!/usr/bin/env bash
# API, integration, end-to-end and mock-server tests with Newman (issue #22, docs/METHODOLOGY.md section 7,
# docs/TECHNICAL_REFERENCE.md section 6 and section 10, api-tests.yml).
# Starts the compose stack from deploy/compose/, runs every collection with environments/ci (the mock tests
# against the local mock server, then once more against the public Postman Mock Server) and the
# cli, junit and htmlextra reporters, and stops the stack again. One failing collection doesn't stop
# the others, but it fails the run. Reports go to newman-reports/.
# Needs docker and newman (with newman-reporter-htmlextra) on PATH.
# Usage: tools/ci/api-tests.sh   (run from the repo root; exit code 0 = every collection passes)
#        API_TESTS_BUILD=1 tools/ci/api-tests.sh   rebuilds the fitlife/*:local images first (`up --build`).
#        Off by default: compose then reuses the images it has, which may be older than your change (#284).
set -uo pipefail
shopt -s nullglob

compose_dir=deploy/compose
postman=tests/postman
environment=$postman/environments/ci.postman_environment.json
reports=newman-reports

# Collections in the order they run. mocks/*.mock.postman_collection.json backs the mock server and isn't a test.
collections=(
  "$postman"/services/*.postman_collection.json
  "$postman"/integration/*.postman_collection.json
  "$postman"/e2e/*.postman_collection.json
  "$postman"/mocks/*.mock-tests.postman_collection.json
)

stop_stack() { (cd "$compose_dir" && docker compose down -v); }

# The made-up passwords in .env.example are what CI uses (deploy/compose/README.md). A local .env is kept.
[ -f "$compose_dir/.env" ] || cp "$compose_dir/.env.example" "$compose_dir/.env"

up=(docker compose up -d --wait)
[ "${API_TESTS_BUILD:-0}" = 1 ] && up+=(--build)

trap stop_stack EXIT
echo "::group::Start the stack"
if ! (cd "$compose_dir" && "${up[@]}"); then
  echo "::endgroup::"
  echo "::error::api-tests: the compose stack in $compose_dir didn't start, so no collection ran."
  exit 1
fi
echo "::endgroup::"

mkdir -p "$reports"
failed=0
for collection in "${collections[@]}"; do
  folder=$(basename "$(dirname "$collection")")
  name="$folder-$(basename "$collection" .postman_collection.json)"
  echo "::group::$collection"
  if ! newman run "$collection" -e "$environment" -r cli,junit,htmlextra \
      --reporter-junit-export "$reports/$name.xml" \
      --reporter-htmlextra-export "$reports/$name.html"; then
    echo "::error::api-tests: $collection failed."
    failed=1
  fi
  echo "::endgroup::"
done

# The mock tests once more against the public Postman Mock Server (environments/local), whose report is the
# evidence the project needs (#279). Postman's cloud is outside this repo, so a failure here only warns.
for collection in "$postman"/mocks/*.mock-tests.postman_collection.json; do
  name="mocks-public-$(basename "$collection" .postman_collection.json)"
  echo "::group::$collection (public Postman Mock Server)"
  if ! newman run "$collection" -e "$postman/environments/local.postman_environment.json" -r cli,junit,htmlextra \
      --reporter-junit-export "$reports/$name.xml" \
      --reporter-htmlextra-export "$reports/$name.html"; then
    echo "::warning::api-tests: $collection failed against the public Postman Mock Server; the local mock run decides the job."
  fi
  echo "::endgroup::"
done

if [ "$failed" -eq 0 ]; then echo "Every collection passed (${#collections[@]} run)."; fi
exit "$failed"
