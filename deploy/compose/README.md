# Local Docker Compose stack

The infrastructure for a laptop and for CI. Services, Newman and the Azure deploy (#10) share this setup.

| Group | File | Contains |
|---|---|---|
| infrastructure | `infrastructure.yml` | MongoDB (one database and one user per service), RabbitMQ with the management UI, Vault in dev mode with the services' secrets |
| services | `services.yml` | The Nginx gateway; services and UIs join here as they are built (#18, #8) |
| monitoring | `monitoring.yml` | Loki and Grafana, with the datasource, dashboards and alert rules as code (#6) |

`compose.yaml` includes every group. Image versions are pinned, never `:latest`.

## Start and stop

From this folder (`deploy/compose/`):

```bash
cp .env.example .env
```

```bash
docker compose up -d --wait
```

`--wait` returns when every container's healthcheck passes. Then:

| What | Where (port + `PORT_OFFSET` when `ports.sh` is sourced, "Two stacks side by side") |
|---|---|
| Gateway | http://localhost:8080 (`/health`, `/api/<context>/...`) |
| Member portal | http://localhost:8080/ through the gateway (the `member-portal` container, #204) |
| RabbitMQ management | http://localhost:15672, user and password from `.env` |
| Vault | http://localhost:8200, log in with `VAULT_DEV_ROOT_TOKEN` from `.env` |
| MongoDB | `mongodb://localhost:27018` (27018, so it doesn't clash with a local MongoDB on 27017) |
| Grafana | http://localhost:8080/grafana/, user and password `GRAFANA_ADMIN_*` from `.env` |
| Loki | http://localhost:3100 (`/ready`, push and query API), for services run outside compose |

Stop, keeping the data:

```bash
docker compose down
```

Stop and delete the data (MongoDB then creates the users again on the next start):

```bash
docker compose down -v
```

**After you change a service, rebuild its image.** `up` builds a `fitlife/*:local` image only when it doesn't exist yet. Once it does, `up` reuses it, so the stack runs your old code. Rebuild with:

```bash
docker compose up -d --wait --build
```

Rebuild and restart one service only, here Booking:

```bash
docker compose up -d --wait --build booking-svc
```

`tools/ci/api-tests.sh` does the same with `API_TESTS_BUILD=1 tools/ci/api-tests.sh`. Without the switch it reuses the images it has, as before. CI starts on a fresh runner with no images, so it builds them either way (#284).

### Pull the images of `develop` instead of building them

Every merge into `develop` builds, scans and pushes the images of the services that need one to Docker Hub as `albertarnason/<service>:develop` and `:sha-<short>` (`.github/workflows/develop-images.yml`, #340). `compose.develop.yml` makes the stack run them instead of building, which is much faster than a first build (most of that time is downloading base images):

```bash
docker compose -f compose.yaml -f compose.develop.yml up -d --wait
```

`pull_policy: always` pulls the newest `:develop` on every `up`. To run one service from your own code, leave the override out and build from source (`docker compose up -d --wait --build`). `:develop` is a moving tag, allowed only in `compose.develop.yml` (`tools/ci/image-versions.sh`); the other compose files, CI and the api-tests build the PR's code from source. A run delivers every service whose `:develop` image is missing or older than its source (it compares with the image's revision label, not with the previous push), so a skipped or failed run is made up by the next one. The released versions (`X.Y.Z`, `latest`) are still the release's alone.

## Run one service from your IDE

Run one service from your IDE or with `dotnet watch`, against the rest of the stack in compose (#525). Every service has a launch profile named `stack`. It runs on the port below and reaches MongoDB (27018), Vault (8200), RabbitMQ (5672), Loki (3100) and the gateway (8080) on your machine. The gateway still runs in compose. You point it at your service with that service's `*_URL` variable, and the other services stay in compose.

| Service in compose | Project | Port of the `stack` profile | Gateway variable |
|---|---|---|---|
| `booking-svc` | `src/BookingService` | 5080 | `BOOKING_URL` |
| `identity-svc` | `src/IdentityService` | 5003 | `IDENTITY_URL` |
| `mcp-server` | `src/McpServer` | 5005 | `MCP_SERVER_URL` |
| `member-portal` | `src/MemberPortal.Web` | 5066 | `MEMBER_PORTAL_URL` |
| `membership-svc` | `src/MembershipService` | 5082 | `MEMBERSHIP_URL` |
| `notification-svc` | `src/NotificationService` | 5083 | `NOTIFICATION_URL` |

Booking, here. From this folder, with the stack up:

1. Stop the service in compose: `docker compose stop booking-svc`
2. Point the gateway at your machine: `BOOKING_URL=http://host.docker.internal:5080 docker compose up -d --wait --no-deps gateway`
3. Run the service. The profile has no Vault token (it's in your `.env`), so pass it in. From the repo root: `Vault__Token=$(sed -n 's/^VAULT_DEV_ROOT_TOKEN=//p' deploy/compose/.env) dotnet watch --project src/BookingService --launch-profile stack`. In an IDE, pick the profile `stack` and add `Vault__Token` to its environment variables there (not in the committed file). McpServer and MemberPortal.Web read no Vault and need no token.

Requests through http://localhost:8080/api/booking/ now reach your service. Go back to the service in compose:

```bash
docker compose up -d --wait --no-deps gateway booking-svc
```

Without `BOOKING_URL` in the shell, the gateway gets its default `http://booking-svc:8080` again. Every `*_URL` in `services.yml` defaults to the service in compose, so `docker compose up` without them runs the stack as before.

- Booking's profile asks Membership through the gateway (`http://localhost:8080/api/membership/`), since `membership-svc` has no port on your machine.
- **Linux:** `host.docker.internal` works only with Docker Desktop. With Docker Engine, use the address of the stack's network gateway instead (`docker network inspect fitlife_default -f '{{(index .IPAM.Config 0).Gateway}}'`), and let the service listen there: `dotnet watch --project src/BookingService --launch-profile stack -- --urls http://0.0.0.0:5080`.

## Local builds: base images

A local build uses the base images already on your machine. The cheapest source comes first:

- **(a)** pull the published `:develop` images from Docker Hub instead of building (`compose.develop.yml`, #340; see "Pull the images of `develop` instead of building them" above);
- **(b)** build with the base images in your local cache. This is what `docker compose up --build` does, and the compose files never force a pull (no `--pull`, no `pull: true`);
- **(c)** download the base images from MCR (`mcr.microsoft.com`) only as a last resort.

A first build with no base images on disk is slow because of MCR, not the build: on 2026-11-25 the first `docker compose up` took about 10 minutes for three images, and the SDK image alone is about 272 MB, downloaded at about 40 KB/s. After that, a rebuild downloads no base images.

**Probe: does a build with the base images on disk pull anything?** This scratch build names its base under an unreachable registry host (`127.0.0.1:9`), so a build that reaches a registry would fail. It passes only when the base comes from the local image store:

```bash
mkdir -p /tmp/local-build-probe && cd /tmp/local-build-probe
docker tag alpine:3.24.4 127.0.0.1:9/mcr/aspnet:10.0-alpine   # any image you have
printf 'FROM 127.0.0.1:9/mcr/aspnet:10.0-alpine\nRUN echo built\n' > Dockerfile
printf 'services:\n  probe:\n    build: .\n    image: probe-local:local\n' > compose.yaml
docker compose build && docker compose build   # both succeed: the second one pulled nothing
cd / && rm -rf /tmp/local-build-probe && docker rmi -f probe-local:local 127.0.0.1:9/mcr/aspnet:10.0-alpine
```

A floating tag (`aspnet:10.0-alpine`, as in `src/MemberPortal.Web/Dockerfile`) doesn't change this: a tag that exists in the local store is used without a registry check. It was measured with the default Docker builder. A `docker-container` builder (e.g. CI's buildx) may differ.

One group on its own: `docker compose -f infrastructure.yml up -d --wait`.

Two stacks side by side (two worktrees, a PR preview): give the second its own project name and shift its ports with `PORT_OFFSET` (#585). Compose can't add numbers, so source `ports.sh` first; it exports every `*_PORT` above as its default + `PORT_OFFSET` and prints the ports:

```bash
cd deploy/compose
PORT_OFFSET=100 . ./ports.sh && docker compose -p fitlife-2 up -d --wait
```

The gateway is then on http://localhost:8180, MongoDB on 28018, and so on. A `*_PORT` already set in your shell wins; `.env`'s `*_PORT` lines are not used while `ports.sh` is sourced. Stop it with `docker compose -p fitlife-2 down` in the same shell. Without `ports.sh` nothing changes (offset 0), as in CI. A Postman run against the second stack needs `--env-var baseUrl=http://localhost:$GATEWAY_PORT`. Inside the stack the containers reach each other on their container ports, so the offset changes only what your machine sees. `port-offset.test.sh` checks that every published port moves.

## Passwords

`.env` (git-ignored) holds every password; the compose files hold none. `.env.example` has made-up values, which CI uses as they are. Change them in your own `.env`. In Azure the passwords are secure Bicep parameters instead (section 9a).

The services don't read `.env`: they get their secrets from Vault (below).

The demo accounts' passwords (`DEMO_*_PASSWORD`) go from `.env` straight to `identity-svc`. They are made-up demo data, not the service's secrets, so they aren't in Vault.

## Vault

Course rule #9: secrets live in Vault, never in code or committed files (#17).

Vault runs in **dev mode** (`hashicorp/vault`, pinned): in memory, unsealed, with one root token, `VAULT_DEV_ROOT_TOKEN` from `.env`. Because a restart empties it, `vault/start.sh` starts the server and then runs `vault/seed.sh`, which writes the values from `.env` into Vault. The healthcheck passes only after that, so a service that waits for Vault to be healthy always finds its secrets.

Each service has one secret, `secret/<name>-svc`, for every name in `SERVICE_DATABASES`. Its keys are configuration keys, so the service can load them into `IConfiguration` as they are:

| Key | From `.env` |
|---|---|
| `MongoDb:Password` | `<NAME>_DB_PASSWORD`, the service's own database user |
| `RabbitMq:Username`, `RabbitMq:Password` | `RABBITMQ_USERNAME`, `RABBITMQ_PASSWORD` |
| `Jwt:SigningKey` | `JWT_SIGNING_KEY` |

**How a service reads it:** `await builder.Configuration.AddVaultAsync()` from `libs/FitLife.Configuration` loads the secret at startup, and the code reads `configuration["MongoDb:Password"]` like any other setting. The service's `appsettings.json` names its secret (`"Vault": { "Path": "booking-svc" }`), and compose gives it `Vault__Address: http://vault:8200` and `Vault__Token` from `VAULT_DEV_ROOT_TOKEN`. Vault and the service start together, so the service waits: while Vault doesn't answer, or answers 404 (not seeded yet) or 5xx, it tries again every second, for up to `Vault:WaitSeconds` (60). A wrong token (403) stops it at once. Either way the log has the HTTP status and the secret's path, never the token. Without `Vault:Address` (`dotnet run`, the tests) nothing is loaded.

Read one by hand:

```bash
docker compose exec vault vault kv get secret/booking-svc
```

A new secret: add it to `.env.example`, to the `vault` service's environment in `infrastructure.yml`, and to `vault/seed.sh`.

`vault/seed.test.sh` is the acceptance check: it starts Vault on its own in a separate compose project, reads every secret back, restarts Vault and checks the secrets are back.

**For the report:** dev-mode Vault keeps its secrets in memory only, has a single root token that every service would share, and serves plain HTTP. That is fine for a laptop and for the exam deploy, where it's seeded from secure parameters on every start, but not for production. Production would run Vault with persistent storage (integrated Raft storage), TLS, auto-unseal and one policy and token (or AppRole) per service, so a service can read only its own `secret/<name>-svc`. On Azure the simpler choice is **Azure Key Vault**, with each container's managed identity given read access to its own secrets.

## MongoDB: one database per service

`mongodb/init/01-service-users.js` runs the first time MongoDB starts with an empty volume. For each name in `SERVICE_DATABASES` it creates the database `<name>` and the user `<name>-svc` with `readWrite` on that database only, so a service can't read another service's data (`docs/TECHNICAL_REFERENCE.md` section 1).

A service's connection string inside the stack:

```
mongodb://booking-svc:<BOOKING_DB_PASSWORD>@mongodb:27017/booking?authSource=booking
```

**MongoDB 8 won't start on Linux kernels 6.19 to 7.0.13** ("MongoDB cannot start: Linux kernel versions 6.19.0 through 7.0.13 have a known incompatibility", MongoDB SERVER-121912). Docker Desktop versions with such a kernel hit this. Check with `docker info --format '{{.KernelVersion}}'`. Update Docker Desktop, or until then add `MONGO_IMAGE=mongo:7.0.48` to your own `.env`.

## The gateway

`nginx/default.conf.template` is rendered by the nginx image's `envsubst` at start, so the upstream addresses are environment variables (`BOOKING_URL`, `MEMBERSHIP_URL`, `IDENTITY_URL`, `MCP_SERVER_URL`, `MEMBER_PORTAL_URL`, `GRAFANA_URL` in `services.yml`). Azure mounts the same file with `http://127.0.0.1:<port>` addresses instead (`deploy/azure/`, #10). Every variable must be set, or Nginx won't start.

- `/api/<context>/<path>` reaches the service as `/<path>`, e.g. `/api/booking/reservations/42` → `http://booking-svc:8080/reservations/42`.
- `/mcp` reaches the MCP server (the `mcp-server` container, #213) with the path unchanged.
- Every other path goes to the member portal (Blazor UI, the `member-portal` container) unchanged, with WebSocket upgrade for Blazor's SignalR connection. Open the member portal at http://localhost:8080/. Running it with `dotnet run` instead, it reaches the stack's APIs through `Gateway:BaseUrl` (`http://localhost:8080` in `appsettings.Development.json`).
- A route whose service isn't running answers 502; the gateway itself still starts.
- Log-in (`POST /api/identity/tokens`) is limited per client address, `LOGIN_RATE_LIMIT` with a burst of `LOGIN_RATE_BURST` (10 a minute and 10 at once in `.env.example`); above that the gateway answers 429 and Identity never sees the request (#366).
- The gateway passes on the caller's `X-Correlation-ID`, or sets a new one, and returns it in the response (section 7).
- `/grafana/` reaches Grafana (`GRAFANA_URL`) with the path unchanged, since Grafana serves itself under `/grafana/`. WebSocket upgrades are passed on (Grafana Live).

## Adding a service

1. Add the service to `services.yml`: name it `<name>-svc`, listen on 8080, give it a `healthcheck` on `/health`, and pass its MongoDB and RabbitMQ hosts, the Vault address and `VAULT_DEV_ROOT_TOKEN` as environment variables. Its passwords come from Vault, at `secret/<name>-svc`.
2. If it has a database: add its name to `SERVICE_DATABASES` in `infrastructure.yml`, add `<NAME>_DB_PASSWORD` there (under `x-service-secrets`) and to `.env.example`, then `docker compose down -v` so the init script runs again. Vault gets the new secret on its next start.
3. If it has an HTTP API: add a `location /api/<context>/` block to the Nginx template and the `<NAME>_URL` variable to the gateway in `services.yml`.
4. Logging: pass `LOKI_URL: http://loki:3100` to the service, log with the labels and fields below, and if `grafana/provisioning/alerting/error-rate.yml` has no rule for its name yet, add an error-rate rule for it there.

## Logging and monitoring

Services log with NLog to Loki; Grafana shows the logs and raises alerts (`docs/TECHNICAL_REFERENCE.md` section 7). Inside the stack a service pushes to `http://loki:3100`; a service started with `dotnet run` pushes to `http://localhost:3100` (`LOKI_HOST_PORT`).

What the dashboard and alert rules expect from a service's log lines (the NLog config comes with the first service, #18):

| Where | Name | Value |
|---|---|---|
| Loki label | `app` | the service's compose name, e.g. `booking-svc` |
| Loki label | `server` | `${hostname}`, tells scaled instances apart |
| Loki label | `version` | `${assembly-version:type=Informational}` |
| JSON body | `level` | NLog's `${level}`: `Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal` (any case) |
| JSON body | `message`, `CorrelationId`, event properties | the rendered message, the request's correlation ID, and properties such as `ReservationId` |

Only those three are labels: Loki indexes labels, so ids belong in the JSON body, where LogQL's `| json` reads them.

Grafana as code, in `grafana/provisioning/` (mounted at `/etc/grafana/provisioning`; Azure mounts the same folder from Azure Files):

| File | What |
|---|---|
| `datasources/loki.yml` | The Loki datasource, uid `loki`. Its URL is `LOKI_URL` on the Grafana container: `http://loki:3100` here, `http://127.0.0.1:3100` in Azure. |
| `dashboards/fitlife.yml`, `dashboards/fitlife/*.json` | Dashboards in the FitLife folder. **FitLife logs**: log lines by service and by level, errors by service, and the log lines themselves, filtered by service, level and CorrelationId. |
| `alerting/error-rate.yml` | One rule per service: more than 5 `Error`/`Fatal` lines in 5 minutes, for 2 minutes. No logs counts as OK. Notifications go to Grafana's default contact point until the team picks a channel. |

Provisioned dashboards and rules can't be saved from the Grafana UI. To change one, edit it in the UI, export the JSON (dashboard: Share → Export; rule: More → Export), put it in the file and open a PR. Grafana reloads dashboards within 30 seconds; datasources and alert rules on restart (`docker compose restart grafana`).

Follow one request: the gateway returns its `X-Correlation-ID` in every response. Paste it into the CorrelationId box on the FitLife logs dashboard to see the request's lines from every service.

Loki keeps logs for 7 days (`loki/loki.yml`). The Loki image has no shell, so the Loki container has no healthcheck of its own; Grafana's healthcheck also requires Loki's `/ready`, so `up --wait` still waits for Loki.
