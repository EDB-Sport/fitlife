# Production on Azure: Bicep + Azure Container Instances

Production runs as one container group on Azure Container Instances (ACI), described in Bicep (`docs/TECHNICAL_REFERENCE.md` section 9a). It holds Booking, Membership and Identity (#371), NotificationService (#261) and the member portal, plus MongoDB, RabbitMQ and Vault (#23). Identity seeds the three demo accounts, one per role, so the public URL can be logged in to, and Booking seeds the demo class schedule (#386, see "Demo data" below). Loki and Grafana join with #6, and the other services and UIs as they are released (#217).

```
internet --HTTPS 443--> caddy --:80--> nginx --/api/<context>/--> services       :5001-5003 --> mongodb  :27017
                                             --everything else--> member portal  :5101           rabbitmq :5672
                                                                                                 vault    :8200
                                                          notification-svc :5004 <-- ReservationMade from rabbitmq
```

| Container | Port inside the group | Notes |
|---|---|---|
| `caddy` | 443 (public), 8081 | Let's Encrypt certificate for `edb-fitlife.switzerlandnorth.azurecontainer.io`, kept on the `caddy-data` file share. Only 443 is public: `http://` is not answered. |
| `nginx` | 80 | The same template as compose: `deploy/compose/nginx/default.conf.template`, mounted as a secret volume. It limits log-in (`POST /api/identity/tokens`) per client address and answers 429 above it (`LOGIN_RATE_LIMIT`, `LOGIN_RATE_BURST` in `modules/containers.bicep`); the address is taken from Caddy's `X-Forwarded-For`, trusted only from 127.0.0.1 (#366). |
| `booking-svc`, `membership-svc`, `identity-svc` | 5001, 5002, 5003 | One container per entry of `services` in `main.bicepparam`, with the settings of its block in `deploy/compose/services.yml` (`127.0.0.1` for the container names, `serviceSettings` in `modules/containers.bicep`). `booking-svc` gets `Seed__Enabled`, as in compose. `identity-svc` also gets `Seed__DemoAccounts` and the three demo passwords, which are secure parameters of their own (`DEMO_MEDLEM_PASSWORD`, `DEMO_TRAENER_PASSWORD`, `DEMO_ADMIN_PASSWORD`), not the values of `.env.example`. |
| `member-portal` | 5101 | The Blazor UI. Only in the group once it has a release (`includeMemberPortal`, set when `UI_IMAGE_TAG` is). Until then Nginx answers 502 on `/` (#198). |
| `notification-svc` | 5004 | NotificationService: consumes `ReservationMade` from RabbitMQ and stores the notification in MongoDB. No route in Nginx. Same settings as compose; `Loki__Url` has nothing behind it until #6. Only in the group once it has a release (`includeNotificationService`, set when `NOTIFICATION_IMAGE_TAG` is). |
| `mongodb` | 27017 | One database and one user per service, created by the compose init script `deploy/compose/mongodb/init/01-service-users.js`. Data on the `mongodb-data` file share. |
| `rabbitmq` | 5672, 15672 | The management UI isn't routed through Nginx, so it's reachable only inside the group. |
| `vault` | 8200 (on 127.0.0.1) | Dev mode, set up by the compose scripts `deploy/compose/vault/start.sh` and `seed.sh`: seeded from the secure parameters on every start, ready only once seeded. See "Vault in production" below. |

Containers in a group share `localhost`, so each one listens on its own port (`ASPNETCORE_HTTP_PORTS`), and Nginx reaches them at `http://127.0.0.1:<port>`. The ports are set in `modules/containers.bicep`. A further service is one more entry in `services` (`main.bicepparam`), with its port in `servicePorts` and its settings in `serviceSettings`.

Every container has `restartPolicy: Always` and a liveness and a readiness probe: `/health` for Nginx and the .NET containers, and a command for the others (Caddy's admin API, `mongosh` ping, `rabbitmq-diagnostics`, `vault status`).

### Demo data

Azure seeds the same demo data as compose, so the public URL isn't empty (decision on #217). Identity seeds the three demo accounts (`Seed__DemoAccounts`, #237). Booking seeds the demo class schedule (`Seed__Enabled`, #255): the centres "FitLife Center 1" to "6", three class types, and two class sessions per centre and day for the next 4 weeks, so `/hold` has class sessions to reserve. Booking's `/health` is unhealthy until the seed has finished.

The seed runs every time `booking-svc` starts, so a redeploy (or a start with azure-power) refreshes it: that run tops the schedule up to 4 weeks from its own start and keeps what is stored, reservations included. The demo data is made up (FitLife is fictional), and nothing in it is a real person: the public URL is open to anyone.

### CPU and memory

A container group has a limit per region: 4 vCPU and 16 GB for Linux containers in `switzerlandnorth`. `topology.test.sh` adds up the requests and fails above it.

| | vCPU | GB |
|---|---|---|
| Today: Caddy, Nginx, Vault (0.25 / 0.3 each, because ACI takes memory in 0.1 GB steps), Booking, Membership, Identity, NotificationService, member portal (0.25 / 0.5 each), RabbitMQ (0.25 / 0.5), MongoDB (0.5 / 1.0). Each of the member portal and NotificationService that isn't released takes 0.25 vCPU and 0.5 GB off | 2.75 | 4.9 |
| Later: one more UI (0.25 / 0.5), Loki and Grafana (0.25 / 0.5 each) | +0.75 | +1.5 |
| Total | 3.5 | 6.4 |

That fits in one group. If it ever doesn't, split it into a second group (section 9a).

### Vault in production

Vault runs in dev mode here too, so its secrets live in memory only and are written again from the secure parameters on every start. For the report: a real production setup would run Vault with storage, TLS, auto-unseal and one policy per service, or use Azure Key Vault with each container's managed identity (`deploy/compose/README.md`, "Vault").

## Files

| File | What |
|---|---|
| `main.bicep` | Entry point (resource group scope). Outputs `publicUrl`. |
| `modules/storage.bicep` | Storage account and Azure Files shares. |
| `modules/containers.bicep` | The container group, probes and volumes. |
| `topology.test.sh` | Offline check of the compiled template: containers, the one public port, probes, CPU and memory, secrets as `secureValue`, file shares, the scripts shared with compose, each service's port and settings against its compose block, when the member portal and NotificationService are in the group, the demo passwords, Booking's seed setting, and the member portal's gateway address. |
| `main.bicepparam` | Production values: region, DNS label, the services with their image names, and the member portal. Secrets and released versions come from environment variables. |
| `caddy/Caddyfile` | Caddy's config, mounted as a secret volume. |
| `budget.bicep` | The monthly cost alert on the subscription. Run once by a person. |
| `bicepconfig.json` | Every linter rule at `error`. |

Image versions are pinned, never `latest`.

## How a deploy runs

`.github/workflows/deploy.yml` runs after `release.yml` succeeds on `main`:

1. waits for one approval on the GitHub environment `production`
2. picks each image's version from its newest release tag (`bookingservice/vX.Y.Z`, `membershipservice/vX.Y.Z`, `identityservice/vX.Y.Z`, `memberportal/vX.Y.Z`, `notificationservice/vX.Y.Z`) with `tools/ci/deploy-versions.sh`, and refuses anything that isn't `X.Y.Z`, or Booking, Membership or Identity without a release. Without a `notificationservice/v*` tag it deploys without NotificationService. Without a `memberportal/v*` tag it deploys without the member portal, and the smoke test skips the portal's checks. Until the portal is released, `/` answers 502: Nginx still routes it to the portal, which isn't in the group
3. logs in to Azure with OpenID Connect (no stored client secret)
4. prints `az deployment group what-if`, then runs `az deployment group create`
5. waits for HTTPS, then runs the Newman smoke test `tests/postman/smoke/thin-slice.postman_collection.json`: the health of each service through the gateway, a log-in as the Medlem demo account (`POST /api/identity/tokens`, a token with the role Medlem), and one data request: Booking's class schedule (`GET /api/booking/class-sessions`, not empty), which fails when Booking can't reach MongoDB (#519)

**Rollback:** run the workflow by hand (Actions → deploy → Run workflow) with the previous release's versions in `booking_tag`, `membership_tag`, `identity_tag`, `ui_tag` and `notification_tag`. An empty one means that image's newest release (for `ui_tag` and `notification_tag`: or none of it in the group while it has no release).

## From your own machine

The same deploy, e.g. if the subscription doesn't allow app registrations for OIDC (then CI only runs the smoke test; record it as a deviation). Log in with `az login` first.

```bash
export DOCKERHUB_USERNAME=<user> DOCKERHUB_PULL_TOKEN=<read-only token> UI_IMAGE_TAG=0.1.0 NOTIFICATION_IMAGE_TAG=0.1.0
export BOOKING_IMAGE_TAG=0.1.0 MEMBERSHIP_IMAGE_TAG=0.1.0 IDENTITY_IMAGE_TAG=0.1.0
```

Leave `UI_IMAGE_TAG` unset to deploy without the member portal, and `NOTIFICATION_IMAGE_TAG` to deploy without NotificationService.

and the secrets, with the same names as in `deploy/compose/.env` (`MONGO_ROOT_PASSWORD`, `BOOKING_DB_PASSWORD`, `MEMBERSHIP_DB_PASSWORD`, `IDENTITY_DB_PASSWORD`, `NOTIFICATION_DB_PASSWORD`, `RABBITMQ_PASSWORD`, `VAULT_DEV_ROOT_TOKEN`, `JWT_SIGNING_KEY`, `DEMO_MEDLEM_PASSWORD`, `DEMO_TRAENER_PASSWORD`, `DEMO_ADMIN_PASSWORD`), but with production values.

```bash
az deployment group what-if --resource-group rg-fitlife-prod --parameters deploy/azure/main.bicepparam
```

```bash
az deployment group create --resource-group rg-fitlife-prod --parameters deploy/azure/main.bicepparam
```

Check without Azure: `az bicep build --file deploy/azure/main.bicep`, `az bicep lint --file deploy/azure/main.bicep` and `deploy/azure/topology.test.sh`.

## One-time setup (a person, in Azure and GitHub)

1. Register the resource providers once per subscription: `Microsoft.ContainerInstance` and `Microsoft.Storage`.
2. Create the resource group `rg-fitlife-prod` in `switzerlandnorth` (the region in `main.bicepparam`; change both if the subscription's policy doesn't allow it).
3. Create an app registration (e.g. `fitlife-github-deploy`) with a federated credential: issuer `https://token.actions.githubusercontent.com`, subject `repo:EDB-Sport@324029458/fitlife@1468529150:environment:production` (the repo uses GitHub's immutable subject, which names the org and repo by ID), audience `api://AzureADTokenExchange`. Give its service principal **Contributor** on `rg-fitlife-prod` only.
4. Create the GitHub environment `production` with required reviewers and deployment branch `main`, and these environment secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `DOCKERHUB_USERNAME`, `DOCKERHUB_PULL_TOKEN` (a Docker Hub access token with read-only scope), and the secrets `MONGO_ROOT_PASSWORD`, `BOOKING_DB_PASSWORD`, `MEMBERSHIP_DB_PASSWORD`, `IDENTITY_DB_PASSWORD`, `NOTIFICATION_DB_PASSWORD`, `RABBITMQ_PASSWORD`, `VAULT_DEV_ROOT_TOKEN`, `JWT_SIGNING_KEY` (at least 32 characters) and the three demo accounts' passwords `DEMO_MEDLEM_PASSWORD`, `DEMO_TRAENER_PASSWORD` and `DEMO_ADMIN_PASSWORD`. Generate each with e.g. `openssl rand -base64 32`; never reuse the made-up values from `.env.example`. The public URL is open to anyone, so the demo passwords must not be the ones in `.env.example`, which is public in the repo. Keep them where the group keeps its passwords, so the presenters can log in.
5. Create the budget alert: `az deployment sub create --location switzerlandnorth --template-file deploy/azure/budget.bicep --parameters contactEmails='["<address>"]'`, or the same in the portal (Cost Management → Budgets).

## Cost

ACI bills per vCPU-second and GB-second while the group runs. With every container in it, the group requests 2.75 vCPU and 4.9 GB in total today, 2.25 vCPU and 3.9 GB without the member portal and NotificationService (see "CPU and memory").

Start and stop the group from GitHub: Actions → azure-power → Run workflow, with `action` `start` or `stop`. Run it from `main` (the `production` environment allows only that branch), and one group member approves every run on that environment, so the run log records when the PoC was up. A start waits until `/health` answers over HTTPS, the same wait as a deploy. With Azure CLI rights you can also stop it from a terminal: `az container stop --resource-group rg-fitlife-prod --name fitlife`.

Keep it running from hand-in (18 Dec) through the exam, and delete the resource group after the exam (`az group delete --name rg-fitlife-prod`).
