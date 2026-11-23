# Production on Azure: Bicep + Azure Container Instances

Production runs as one container group on Azure Container Instances (ACI), described in Bicep (`docs/CSHARP_SETUP_TODO.md` section 9a). Today it is the thin slice from #10; services, MongoDB, RabbitMQ, Vault, Loki and Grafana join the same group as they are released.

```
internet --HTTPS 443--> caddy --:80--> nginx --/api/<context>/--> service        :5001-5003
                                             --everything else--> member portal  :5101
```

| Container | Port inside the group | Notes |
|---|---|---|
| `caddy` | 443 (public), 8081 | Let's Encrypt certificate for `<dnsNameLabel>.<region>.azurecontainer.io`, kept on the `caddy-data` file share. Only 443 is public: `http://` is not answered. |
| `nginx` | 80 | The same template as compose: `deploy/compose/nginx/default.conf.template`, mounted as a secret volume. |
| `<context>-svc` | 5001 booking, 5002 membership, 5003 identity | One service in the thin slice (`serviceContext`). The others' routes answer 502. |
| `member-portal` | 5101 | The Blazor UI. |

Containers in a group share `localhost`, so each one listens on its own port (`ASPNETCORE_HTTP_PORTS`), and Nginx reaches them at `http://127.0.0.1:<port>`. The ports are set in `modules/containers.bicep`.

## Files

| File | What |
|---|---|
| `main.bicep` | Entry point (resource group scope). Outputs `publicUrl` and `serviceBasePath`. |
| `modules/storage.bicep` | Storage account and Azure Files shares. |
| `modules/containers.bicep` | The container group, probes and volumes. |
| `main.bicepparam` | Production values: region, DNS label, image names and versions. Secrets and released versions come from environment variables. |
| `caddy/Caddyfile` | Caddy's config, mounted as a secret volume. |
| `budget.bicep` | The monthly cost alert on the subscription. Run once by a person. |
| `bicepconfig.json` | Every linter rule at `error`. |

Image versions are pinned, never `latest`.

## How a deploy runs

`.github/workflows/deploy.yml` runs after `release.yml` succeeds on `main`:

1. waits for one approval on the GitHub environment `production`
2. picks the versions from the release tags (`bookingservice/vX.Y.Z`, `memberportal/vX.Y.Z`) and refuses anything that isn't `X.Y.Z`
3. logs in to Azure with OpenID Connect (no stored client secret)
4. prints `az deployment group what-if`, then runs `az deployment group create`
5. waits for HTTPS, then runs the Newman smoke test `tests/postman/smoke/thin-slice.postman_collection.json`

**Rollback:** run the workflow by hand (Actions → deploy → Run workflow) with the previous release's versions.

## From your own machine

The same deploy, e.g. if the subscription doesn't allow app registrations for OIDC (then CI only runs the smoke test; record it as a deviation). Log in with `az login` first.

```bash
export DOCKERHUB_USERNAME=<user> DOCKERHUB_PULL_TOKEN=<read-only token> SERVICE_IMAGE_TAG=0.1.0 UI_IMAGE_TAG=0.1.0
```

```bash
az deployment group what-if --resource-group rg-fitlife-prod --parameters deploy/azure/main.bicepparam
```

```bash
az deployment group create --resource-group rg-fitlife-prod --parameters deploy/azure/main.bicepparam
```

Check without Azure: `az bicep build --file deploy/azure/main.bicep` and `az bicep lint --file deploy/azure/main.bicep`.

## One-time setup (a person, in Azure and GitHub)

1. Register the resource providers once per subscription: `Microsoft.ContainerInstance` and `Microsoft.Storage`.
2. Create the resource group `rg-fitlife-prod` in `northeurope` (the region in `main.bicepparam`; change both if the subscription's policy doesn't allow it).
3. Create an app registration (e.g. `fitlife-github-deploy`) with a federated credential: issuer `https://token.actions.githubusercontent.com`, subject `repo:EDB-Sport/fitlife:environment:production`, audience `api://AzureADTokenExchange`. Give its service principal **Contributor** on `rg-fitlife-prod` only.
4. Create the GitHub environment `production` with required reviewers and deployment branch `main`, and these environment secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `DOCKERHUB_USERNAME`, `DOCKERHUB_PULL_TOKEN` (a Docker Hub access token with read-only scope).
5. Create the budget alert: `az deployment sub create --location northeurope --template-file deploy/azure/budget.bicep --parameters contactEmails='["<address>"]'`, or the same in the portal (Cost Management → Budgets).

## Cost

ACI bills per vCPU-second and GB-second while the group runs. The thin slice requests 1 vCPU and 1.5 GB in total. Stop the group when nobody needs it (`az container stop --resource-group rg-fitlife-prod --name fitlife`), keep it running from hand-in (18 Dec) through the exam, and delete the resource group after the exam (`az group delete --name rg-fitlife-prod`).
