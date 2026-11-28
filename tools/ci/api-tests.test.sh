#!/usr/bin/env bash
# Tests for tools/ci/api-tests.sh (issue #22). Each case runs the script in a fixture repo with stub
# `docker` and `newman` commands that log their arguments, so no Docker or Node is needed.
# Usage: tools/ci/api-tests.test.sh   (exit code 0 = every case passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
script="$here/api-tests.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0
# A developer who exports API_TESTS_BUILD must not change the cases that run without it.
unset API_TESTS_BUILD

# Stubs: each call is one line in $work/calls. newman fails for a collection named *failing*,
# `docker compose up` fails when STUB_UP_FAILS=1.
mkdir -p "$work/bin"
cat > "$work/bin/docker" <<'EOF'
#!/usr/bin/env bash
echo "docker $* (in ${PWD##*/})" >> "$CALLS"
if [ "${STUB_UP_FAILS:-0}" = 1 ] && [[ " $* " == *" up "* ]]; then exit 1; fi
exit 0
EOF
cat > "$work/bin/newman" <<'EOF'
#!/usr/bin/env bash
echo "newman $*" >> "$CALLS"
[[ "$*" == *failing* ]] && exit 1
exit 0
EOF
chmod +x "$work/bin/docker" "$work/bin/newman"

# fixture: a repo with the compose folder and the Postman layout. Prints its path.
fixture() {
  local repo
  repo=$(mktemp -d "$work/repo.XXXXXX")
  mkdir -p "$repo/deploy/compose" "$repo/tests/postman/"{services,integration,e2e,mocks,environments}
  echo "PASSWORD=made-up" > "$repo/deploy/compose/.env.example"
  touch "$repo/deploy/compose/compose.yaml" "$repo/tests/postman/environments/ci.postman_environment.json"
  touch "$repo/tests/postman/services/.gitkeep" "$repo/tests/postman/integration/.gitkeep" "$repo/tests/postman/e2e/.gitkeep"
  echo '{}' > "$repo/tests/postman/mocks/user-registration.mock.postman_collection.json"
  echo '{}' > "$repo/tests/postman/mocks/user-registration.mock-tests.postman_collection.json"
  echo "$repo"
}

# add <repo> <folder> <name>: a collection in tests/postman/<folder>/
add() { echo '{}' > "$1/tests/postman/$2/$3.postman_collection.json"; }

# run <repo>: runs the script, sets $status, $out and $calls
run() {
  : > "$work/calls"
  out=$(cd "$1" && PATH="$work/bin:$PATH" CALLS="$work/calls" "$script" 2>&1)
  status=$?
  calls=$(cat "$work/calls")
}

# check <description> <condition...>
check() {
  local description=$1
  shift
  if "$@"; then
    echo "PASS  $description"
  else
    echo "FAIL  $description"
    echo "$out" | sed 's/^/      out: /'
    echo "$calls" | sed 's/^/      call: /'
    failures=$((failures + 1))
  fi
}
has_call() { grep -qE -- "$1" <<<"$calls"; }
no_call() { ! grep -qE -- "$1" <<<"$calls"; }
call_count() { [ "$(grep -cE -- "$1" <<<"$calls")" -eq "$2" ]; }

# 1. The stack: up with --wait in deploy/compose, down -v at the end, .env from .env.example
repo=$(fixture)
add "$repo" services booking
add "$repo" integration booking-membership
add "$repo" e2e booking-flow
run "$repo"
check "passes when every collection passes" [ "$status" -eq 0 ]
check "starts the stack with up -d --wait in deploy/compose" has_call '^docker compose up -d --wait \(in compose\)$'
check "stops the stack with down -v" has_call '^docker compose down -v \(in compose\)$'
check "stops the stack last" bash -c '[[ "$(tail -1 <<<"$1")" == "docker compose down -v"* ]]' _ "$calls"
check "uses .env.example as .env, changing only the CI log-in burst (#556)" bash -c 'diff <(grep -v "^LOGIN_RATE_BURST=\|^$" "$1") <(grep -v "^LOGIN_RATE_BURST=\|^$" "$2") >/dev/null' _ "$repo/deploy/compose/.env.example" "$repo/deploy/compose/.env"

# 2. The collections, with the ci environment and the three reporters
check "runs services/" has_call '^newman run tests/postman/services/booking\.postman_collection\.json '
check "runs integration/" has_call '^newman run tests/postman/integration/booking-membership\.postman_collection\.json '
check "runs e2e/" has_call '^newman run tests/postman/e2e/booking-flow\.postman_collection\.json '
check "runs the mock tests" has_call '^newman run tests/postman/mocks/user-registration\.mock-tests\.postman_collection\.json '
check "doesn't run the collection behind the mock server" no_call 'user-registration\.mock\.postman_collection'
check "runs 4 collections, skipping .gitkeep" call_count '^newman run' 4
check "every run uses environments/ci" call_count '^newman run .* -e tests/postman/environments/ci\.postman_environment\.json( |$)' 4
check "every run uses the cli, junit and htmlextra reporters" call_count '^newman run .* -r cli,junit,htmlextra( |$)' 4
check "the HTML report goes to newman-reports/" has_call '--reporter-htmlextra-export newman-reports/e2e-booking-flow\.html'
check "the JUnit report goes to newman-reports/" has_call '--reporter-junit-export newman-reports/e2e-booking-flow\.xml'

# 3. A failing collection fails the job, and the other collections still run
repo=$(fixture)
add "$repo" services failing
add "$repo" services membership
run "$repo"
check "a failing collection fails the job" [ "$status" -ne 0 ]
check "the collections after it still run" has_call 'services/membership\.postman_collection\.json'
check "the stack is stopped after a failure" has_call '^docker compose down -v'

# 4. The stack doesn't start: no collection runs, the job fails, and the stack is stopped
repo=$(fixture)
add "$repo" services booking
STUB_UP_FAILS=1 run "$repo"
check "a stack that doesn't start fails the job" [ "$status" -ne 0 ]
check "no collection runs without the stack" no_call '^newman'
check "the stack is stopped after a failed start" has_call '^docker compose down -v'

# 5. API_TESTS_BUILD=1 rebuilds the fitlife/*:local images; without it the command is the same as before (#284)
repo=$(fixture)
add "$repo" services booking
API_TESTS_BUILD=1 run "$repo"
check "API_TESTS_BUILD=1 starts the stack with up -d --wait --build" has_call '^docker compose up -d --wait --build \(in compose\)$'
check "API_TESTS_BUILD=1 still passes" [ "$status" -eq 0 ]
repo=$(fixture)
add "$repo" services booking
API_TESTS_BUILD=0 run "$repo"
check "API_TESTS_BUILD=0 doesn't build" has_call '^docker compose up -d --wait \(in compose\)$'

# 6. Every collection logs in, and the gateway's log-in burst (.env.example: 10) must cover all of them
#    in one run, or the collection after the limit gets 429 (#556). 11 log-ins here, one more than .env.example allows.
repo=$(fixture)
printf 'LOGIN_RATE_BURST=10\n' >> "$repo/deploy/compose/.env.example"
logins=$(for _ in $(seq 11); do printf '{"request":{"method":"POST","url":{"raw":"{{baseUrl}}/api/identity/tokens"}}},'; done)
echo "{\"item\":[${logins%,}]}" > "$repo/tests/postman/services/logins.postman_collection.json"
run "$repo"
burst=$(sed -n 's/^LOGIN_RATE_BURST=//p' "$repo/deploy/compose/.env")
logins_in_run=$(grep -ho 'identity/tokens' "$repo"/tests/postman/*/*.postman_collection.json | wc -l)
check "the CI stack's log-in burst covers every log-in of one run" bash -c '[ -n "$1" ] && [ "$1" -ge "$2" ]' _ "$burst" "$logins_in_run"
check "the run with 11 log-ins still passes" [ "$status" -eq 0 ]

# 6b. The repo's own collections: the burst the CI stack gets covers the log-ins they make in one run (#556)
repo=$(fixture)
for collection in "$here"/../../tests/postman/*/*.postman_collection.json; do
  mkdir -p "$repo/tests/postman/$(basename "$(dirname "$collection")")"
  cp "$collection" "$repo/tests/postman/$(basename "$(dirname "$collection")")/"
done
run "$repo"
burst=$(sed -n 's/^LOGIN_RATE_BURST=//p' "$repo/deploy/compose/.env")
repo_logins=$(grep -ho 'identity/tokens' "$here"/../../tests/postman/*/*.postman_collection.json | wc -l)
check "the CI burst covers the log-ins of this repo's collections in one run ($repo_logins)" bash -c '[ -n "$1" ] && [ "$1" -ge "$2" ]' _ "$burst" "$repo_logins"
check "this repo's collections pass with the CI burst" [ "$status" -eq 0 ]

# 7. The repo's own collections assert status codes and bodies, never response times: a timing
#    assertion against a mock server on the internet depends on the network, not on the code (#135)
timed=$(grep -rlE 'responseTime' "$here/../../tests/postman" --include='*.postman_collection.json' || true)
check "no collection asserts a response time" [ -z "$timed" ]

exit "$failures"
