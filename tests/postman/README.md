# Postman collections

Run by Newman (`docs/METHODOLOGY.md` section 7). Layout from `docs/CSHARP_SETUP_TODO.md` section 6:

```
services/<service>.postman_collection.json         API tests, one per service: happy path + at least one error case per endpoint
integration/<a>-<b>.postman_collection.json         two or more services working together
mocks/<system>.mock.postman_collection.json         saved examples that back a Postman Mock Server
mocks/<system>.mock-tests.postman_collection.json   API tests against that mock server
e2e/booking-flow.postman_collection.json            log in → find a class session → reserve → see it → cancel
smoke/thin-slice.postman_collection.json            after each Azure deploy: HTTPS gateway, the service, the member portal
environments/local|ci|azure.postman_environment.json
```

Secrets never go in an environment file. They go in GitHub Actions secrets.

## Mock server: account registration

Creating accounts isn't built yet, so a Postman Mock Server stands in for it. Services and the UI can be built against it now.

| Request | Example (`x-mock-response-name`) | Response |
|---|---|---|
| `POST /accounts` | `account-created` | 201, the new account and a `Location` header |
| `POST /accounts` | `account-invalid` | 400, ProblemDetails with the invalid fields in `errors` |
| `POST /accounts` | `account-email-taken` | 409, ProblemDetails |
| `GET /accounts/{id}` | `account-found` | 200, the account (never the password) |
| `GET /accounts/{id}` | `account-not-found` | 404, ProblemDetails |

Without the header, Postman picks the example that best matches the request.

**Create the mock server once** (it lives in a Postman account, so a person does this):
1. In Postman: **Import** → `mocks/account-registration.mock.postman_collection.json`.
2. Right-click the collection: **More → Create mock server**. Name it `FitLife account registration`, leave **Make mock server private** unticked (the data is made up, so CI needs no Postman API key), and create it.
3. Copy the mock URL (`https://<id>.mock.pstmn.io`) and put it in `accountRegistrationMockUrl` in all three files in `environments/`.

The mock server exists: `https://688e6cb8-8645-488f-922d-b533f91aec84.mock.pstmn.io` (in Lucas's Workspace). The steps above are only needed if it is ever recreated.

A service that calls the mock gets the URL through configuration, never in code, e.g. `AccountRegistration__BaseUrl=https://<id>.mock.pstmn.io`.

When you change the mock: edit the examples in Postman, export the collection (v2.1) over the file in `mocks/`, and update the mock tests to match. The tests assert status codes, headers and bodies, never response times: the mock server is on the internet, so its timing depends on the network, not on our code (`tools/ci/api-tests.test.sh` checks this, #135). Watch the Postman plan's monthly mock-call limit. If it gets tight, run the mock tests on PRs only.

## Running

```bash
npx newman run tests/postman/mocks/account-registration.mock-tests.postman_collection.json \
  -e tests/postman/environments/local.postman_environment.json
```

Every collection against the compose stack, the way CI runs it (needs Docker and `npm install --global newman newman-reporter-htmlextra`):

```bash
tools/ci/api-tests.sh
```

It starts the stack from `deploy/compose/`, runs `services/`, `integration/`, `e2e/` and the `mocks/*.mock-tests.*` collections with `environments/ci` and the `cli`, `junit` and `htmlextra` reporters, and stops the stack again. The reports go to `newman-reports/`. The `api-tests` CI job uploads them as evidence (issue #22).
