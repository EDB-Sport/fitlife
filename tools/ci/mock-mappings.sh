#!/usr/bin/env bash
# Turns a Postman mock collection into WireMock mappings, so the local mock server in compose (deploy/compose,
# service mock-server) serves the same saved examples as the Postman Mock Server (issue #279).
# Usage: tools/ci/mock-mappings.sh <collection.json>   prints the mappings for one collection
#        tools/ci/mock-mappings.sh --write             rewrites deploy/compose/mock-server/mappings/<name>.json for every
#                                                      tests/postman/mocks/<name>.mock.postman_collection.json
# Each saved example is picked as the Postman Mock Server picks it:
#   - header x-mock-response-name: the example with that name (priority 1)
#   - header x-mock-response-code: an example with that status code (priority 2)
#   - no header: the first example saved for that method and path (priority 5)
# The path is the example's own request path (its {{variable}} base and query string dropped), so two examples of one
# request can answer on two paths (GET /users/<known id> and /users/<unknown id>). Disabled headers are left out.
# tools/ci/mock-mappings.test.sh fails when the committed mappings differ from what this prints. Needs jq.
set -uo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)

mappings() {
  jq --indent 2 '
    # every request item, also inside folders
    [.. | objects | select(has("request") and has("response"))] as $items
    | [ $items[] as $item | $item.response | to_entries[] | {
          index: .key,
          name: .value.name,
          method: (.value.originalRequest.method // $item.request.method),
          path: ((.value.originalRequest.url.raw // .value.originalRequest.url // $item.request.url.raw // $item.request.url)
                 | sub("^\\{\\{[^}]+\\}\\}"; "") | sub("\\?.*$"; "")),
          response: ({ status: .value.code, body: (.value.body // "") }
                     + { headers: ([.value.header[]? | select(.disabled != true) | { (.key): .value }] | add // {}) })
        } ] as $examples
    | { mappings: [
          ($examples[] | { priority: 1,
              request: { method, urlPath: .path, headers: { "x-mock-response-name": { equalTo: .name } } },
              response }),
          ($examples[] | { priority: 2,
              request: { method, urlPath: .path, headers: { "x-mock-response-code": { equalTo: (.response.status | tostring) } } },
              response }),
          ($examples | group_by([.method, .path]) | .[] | min_by(.index) | { priority: 5,
              request: { method, urlPath: .path },
              response })
        ] }
  ' "$1"
}

case ${1-} in
  "")
    echo "usage: tools/ci/mock-mappings.sh <collection.json> | --write" >&2
    exit 2
    ;;
  --write)
    mkdir -p "$root/deploy/compose/mock-server/mappings"
    for collection in "$root"/tests/postman/mocks/*.mock.postman_collection.json; do
      name=$(basename "$collection" .mock.postman_collection.json)
      mappings "$collection" >"$root/deploy/compose/mock-server/mappings/$name.json" || exit 1
      echo "wrote deploy/compose/mock-server/mappings/$name.json"
    done
    ;;
  *) mappings "$1" ;;
esac
