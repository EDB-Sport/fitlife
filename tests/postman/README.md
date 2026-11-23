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

## Mock server: user registration

Creating users isn't built yet, so a Postman Mock Server stands in for it. Services and the UI can be built against it now.

| Request | Example (`x-mock-response-name`) | Response |
|---|---|---|
| `POST /users` | `user-created` | 201, the new user and a `Location` header |
| `POST /users` | `user-invalid` | 400, ProblemDetails with the invalid fields in `errors` |
| `POST /users` | `user-email-taken` | 409, ProblemDetails |
| `GET /users/{id}` | `user-found` | 200, the user (never the password) |
| `GET /users/{id}` | `user-not-found` | 404, ProblemDetails |

Without the header, Postman picks the example that best matches the request.

**Create the mock server once** (it lives in a Postman account, so a person does this):
1. In Postman: **Import** → `mocks/user-registration.mock.postman_collection.json`.
2. Right-click the collection: **More → Create mock server**. Name it `FitLife user registration`, leave **Make mock server private** unticked (the data is made up, so CI needs no Postman API key), and create it.
3. Copy the mock URL (`https://<id>.mock.pstmn.io`) and put it in `userRegistrationMockUrl` in all three files in `environments/`.

The mock server exists: `https://688e6cb8-8645-488f-922d-b533f91aec84.mock.pstmn.io` (in Lucas's Workspace). The steps above are only needed if it is ever recreated.

A service that calls the mock gets the URL through configuration, never in code, e.g. `UserRegistration__BaseUrl=https://<id>.mock.pstmn.io`.

When you change the mock: edit the examples in Postman, export the collection (v2.1) over the file in `mocks/`, and update the mock tests to match. Watch the Postman plan's monthly mock-call limit. If it gets tight, run the mock tests on PRs only.

## Running

```bash
npx newman run tests/postman/mocks/user-registration.mock-tests.postman_collection.json \
  -e tests/postman/environments/local.postman_environment.json
```

CI runs every collection with the `cli`, `junit` and `htmlextra` reporters and uploads the HTML reports as evidence (issue #5).
