# Production on Azure: Bicep + Azure Container Instances

Production runs as one container group on Azure Container Instances (ACI), described in Bicep (`docs/CSHARP_SETUP_TODO.md` section 9a). It holds the thin slice from #10 plus MongoDB, RabbitMQ and Vault (#23). Loki and Grafana join with #6, and the other services and UIs as they are released.

```
internet --HTTPS 443--> caddy --:80--> nginx --/api/<context>/--> service        :5001-5003 --> mongodb  :27017
                                             --everything else--> member portal  :5101           rabbitmq :5672
                                                                                                 vault    :8200
```

| Container | Port inside the group | Notes |
|---|---|---|
| `caddy` | 443 (public), 8081 | Let's Encrypt certificate for `edb-fitlife.switzerlandnorth.azurecontainer.io`, kept on the `caddy-data` file share. Only 443 is public: `http://` is not answered. |
| `nginx` | 80 | The same template as compose: `deploy/compose/nginx/default.conf.template`, mounted as a secret volume. |
| `<context>-svc` | 5001 booking, 5002 membership, 5003 identity | One service in the thin slice (`serviceContext`). The others' routes answer 502. |
| `member-portal` | 5101 | The Blazor UI. Only in the group once it has a release (`includeMemberPortal`, set when `UI_IMAGE_TAG` is). Until then Nginx answers 502 on `/` (#198). |
| `mongodb` | 27017 | One database and one user per service, created by the compose init script `deploy/compose/mongodb/init/01-service-users.js`. Data on the `mongodb-data` file share. |
| `rabbitmq` | 5672, 15672 | The management UI isn't routed through Nginx, so it's reachable only inside the group. |
| `vault` | 8200 (on 127.0.0.1) | Dev mode, set up by the compose scripts `deploy/compose/vault/start.sh` and `seed.sh`: seeded from the secure parameters on every start, ready only once seeded. See "Vault in production" below. |

Containers in a group share `localhost`, so each one listens on its own port (`ASPNETCORE_HTTP_PORTS`), and Nginx reaches them at `http://127.0.0.1:<port>`. The ports are set in `modules/containers.bicep`.

Every container has `restartPolicy: Always` and a liveness and a readiness probe: `/health` for Nginx and the .NET containers, and a command for the others (Caddy's admin API, `mongosh` ping, `rabbitmq-diagnostics`, `vault status`).

### CPU and memory

A container group has a limit per region: 4 vCPU and 16 GB for Linux containers in `switzerlandnorth`. `topology.test.sh` adds up the requests and fails above it.

| | vCPU | GB |
|---|---|---|
| Today: Caddy, Nginx, Vault (0.25 / 0.3 each, because ACI takes memory in 0.1 GB steps), one service, member portal (0.25 / 0.5 each), RabbitMQ (0.25 / 0.5), MongoDB (0.5 / 1.0). Without the member portal: 1.75 vCPU, 2.9 GB | 2.0 | 3.4 |
| Later: three more services and one more UI (0.25 / 0.5 each), Loki and Grafana (0.25 / 0.5 each) | +1.5 | +3.0 |
| Total | 3.5 | 6.4 |

That fits in one group. If it ever doesn't, split it into a second group (section 9a).

### Vault in production

Vault runs in dev mode here too, so its secrets live in memory only and are written again from the secure parameters on every start. For the report: a real production setup would run Vault with storage, TLS, auto-unseal and one policy per service, or use Azure Key Vault with each container's managed identity (`deploy/compose/README.md`, "Vault").

## Files

| File | What |
|---|---|
| `main.bicep` | Entry point (resource group scope). Outputs `publicUrl` and `serviceBasePath`. |
| `modules/storage.bicep` | Storage account and Azure Files shares. |
| `modules/containers.bicep` | The container group, probes and volumes. |
| `topology.test.sh` | Offline check of the compiled template: containers, the one public port, probes, CPU and memory, secrets as `secureValue`, file shares, and the scripts shared with compose. |
| `main.bicepparam` | Production values: region, DNS label, image names and versions. Secrets and released versions come from environment variables. |
| `caddy/Caddyfile` | Caddy's config, mounted as a secret volume. |
| `budget.bicep` | The monthly cost alert on the subscription. Run once by a person. |
| `bicepconfig.json` | Every linter rule at `error`. |

Image versions are pinned, never `latest`.

## How a deploy runs

`.github/workflows/deploy.yml` runs after `release.yml` succeeds on `main`:

1. waits for one approval on the GitHub environment `production`
2. picks the versions from the release tags (`bookingservice/vX.Y.Z`, `memberportal/vX.Y.Z`) and refuses anything that isn't `X.Y.Z`. Without a `memberportal/v*` tag it deploys without the member portal, and the smoke test skips the portal's checks. Until the portal is released, `/` answers 502: Nginx still routes it to the portal, which isn't in the group
3. logs in to Azure with OpenID Connect (no stored client secret)
4. prints `az deployment group what-if`, then runs `az deployment group create`
5. waits for HTTPS, then runs the Newman smoke test `tests/postman/smoke/thin-slice.postman_collection.json`

**Rollback:** run the workflow by hand (Actions → deploy → Run workflow) with the previous release's versions. An empty `ui_tag` means the newest `memberportal/v*` release, or no member portal while it has none.

## From your own machine

The same deploy, e.g. if the subscription doesn't allow app registrations for OIDC (then CI only runs the smoke test; record it as a deviation). Log in with `az login` first.

```bash
export DOCKERHUB_USERNAME=<user> DOCKERHUB_PULL_TOKEN=<read-only token> SERVICE_IMAGE_TAG=0.1.0 UI_IMAGE_TAG=0.1.0
```

Leave `UI_IMAGE_TAG` unset to deploy without the member portal.

and the secrets, with the same names as in `deploy/compose/.env` (`MONGO_ROOT_PASSWORD`, `BOOKING_DB_PASSWORD`, `MEMBERSHIP_DB_PASSWORD`, `IDENTITY_DB_PASSWORD`, `NOTIFICATION_DB_PASSWORD`, `RABBITMQ_PASSWORD`, `VAULT_DEV_ROOT_TOKEN`, `JWT_SIGNING_KEY`), but with production values.

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
3. Create an app registration (e.g. `fitlife-github-deploy`) with a federated credential: issuer `https://token.actions.githubusercontent.com`, subject `repo:EDB-Sport/fitlife:environment:production`, audience `api://AzureADTokenExchange`. Give its service principal **Contributor** on `rg-fitlife-prod` only.
4. Create the GitHub environment `production` with required reviewers and deployment branch `main`, and these environment secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `DOCKERHUB_USERNAME`, `DOCKERHUB_PULL_TOKEN` (a Docker Hub access token with read-only scope), and the secrets `MONGO_ROOT_PASSWORD`, `BOOKING_DB_PASSWORD`, `MEMBERSHIP_DB_PASSWORD`, `IDENTITY_DB_PASSWORD`, `NOTIFICATION_DB_PASSWORD`, `RABBITMQ_PASSWORD`, `VAULT_DEV_ROOT_TOKEN` and `JWT_SIGNING_KEY` (at least 32 characters). Generate each with e.g. `openssl rand -base64 32`; never reuse the made-up values from `.env.example`.
5. Create the budget alert: `az deployment sub create --location switzerlandnorth --template-file deploy/azure/budget.bicep --parameters contactEmails='["<address>"]'`, or the same in the portal (Cost Management → Budgets).

## Cost

ACI bills per vCPU-second and GB-second while the group runs. The group requests 2 vCPU and 3.4 GB in total today, 1.75 vCPU and 2.9 GB without the member portal (see "CPU and memory"). Stop the group when nobody needs it (`az container stop --resource-group rg-fitlife-prod --name fitlife`), keep it running from hand-in (18 Dec) through the exam, and delete the resource group after the exam (`az group delete --name rg-fitlife-prod`).
