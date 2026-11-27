# Postman collections

Run by Newman (`docs/METHODOLOGY.md` section 7). Layout from `docs/TECHNICAL_REFERENCE.md` section 6:

```
services/<service>.postman_collection.json         API tests, one per service: happy path + at least one error case per endpoint
integration/<a>-<b>.postman_collection.json         two or more services working together
mocks/<system>.mock.postman_collection.json         saved examples that back a Postman Mock Server
mocks/<system>.mock-tests.postman_collection.json   API tests against that mock server
e2e/booking-flow.postman_collection.json            log in → find a class session → reserve → see it → cancel
smoke/thin-slice.postman_collection.json            after each Azure deploy: HTTPS gateway, Booking, Membership and Identity, the log-in of the Medlem demo account, the member portal (skipped while it has no release)
environments/local|ci|azure.postman_environment.json
```

Secrets never go in an environment file. They go in GitHub Actions secrets. The demo accounts' passwords in `local` and `ci` are not secrets. They are made-up values, the same as in `deploy/compose/.env.example`, and they only open a stack that was seeded with them.

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

## Mock server: payment provider

The payment provider is outside FitLife Digital (CONTEXT-MAP section 2), so a Postman Mock Server stands in for it (#174). Membership calls it through its anticorruption layer, `src/MembershipService/PaymentProvider/PaymentProviderClient.cs`, the only class that knows the provider's field names (`customer_reference`, `amount_minor`, `outcome`, `decline_code`).

| Request | Example (`x-mock-response-name`) | Response |
|---|---|---|
| `POST /v1/charges` | `charge-approved` | 201, `outcome: approved`. Membership: the fee is paid, the membership stays Aktiv |
| `POST /v1/charges` | `charge-declined` | 402, `outcome: declined` with a `decline_code`. Membership: Betaling mangler |
| `POST /v1/charges` | `provider-error` | 503, `error`. Membership: Betaling mangler, since the fee isn't paid |

**Not created yet.** It must live in the group's shared Postman workspace (#174), so a person creates it with the steps under "Mock server: account registration", from `mocks/payment-provider.mock.postman_collection.json`, named `FitLife payment provider`, and puts the URL in `paymentProviderMockUrl` in the three environments. Then the mock-tests collection and `PaymentProvider__BaseUrl` for compose follow in the next PR for #174.

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
