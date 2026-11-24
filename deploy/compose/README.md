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

| What | Where |
|---|---|
| Gateway | http://localhost:8080 (`/health`, `/api/<context>/...`) |
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

One group on its own: `docker compose -f infrastructure.yml up -d --wait`.

Two stacks side by side (two worktrees, say): give each its own project name and ports, e.g. `docker compose -p fitlife-6 up -d --wait` with other `*_PORT` values in that folder's `.env`.

## Passwords

`.env` (git-ignored) holds every password; the compose files hold none. `.env.example` has made-up values, which CI uses as they are. Change them in your own `.env`. In Azure the passwords are secure Bicep parameters instead (section 9a).

The services don't read `.env`: they get their secrets from Vault (below).

## Vault

Course rule #9: secrets live in Vault, never in code or committed files (#17).

Vault runs in **dev mode** (`hashicorp/vault`, pinned): in memory, unsealed, with one root token, `VAULT_DEV_ROOT_TOKEN` from `.env`. Because a restart empties it, `vault/start.sh` starts the server and then runs `vault/seed.sh`, which writes the values from `.env` into Vault. The healthcheck passes only after that, so a service that waits for Vault to be healthy always finds its secrets.

Each service has one secret, `secret/<name>-svc`, for every name in `SERVICE_DATABASES`. Its keys are configuration keys, so the service can load them into `IConfiguration` as they are:

| Key | From `.env` |
|---|---|
| `MongoDb:Password` | `<NAME>_DB_PASSWORD`, the service's own database user |
| `RabbitMq:Username`, `RabbitMq:Password` | `RABBITMQ_USERNAME`, `RABBITMQ_PASSWORD` |
| `Jwt:SigningKey` | `JWT_SIGNING_KEY` |

Read one by hand:

```bash
docker compose exec vault vault kv get secret/booking-svc
```

A new secret: add it to `.env.example`, to the `vault` service's environment in `infrastructure.yml`, and to `vault/seed.sh`.

`vault/seed.test.sh` is the acceptance check: it starts Vault on its own in a separate compose project, reads every secret back, restarts Vault and checks the secrets are back.

**For the report:** dev-mode Vault keeps its secrets in memory only, has a single root token that every service would share, and serves plain HTTP. That is fine for a laptop and for the exam deploy, where it's seeded from secure parameters on every start, but not for production. Production would run Vault with persistent storage (integrated Raft storage), TLS, auto-unseal and one policy and token (or AppRole) per service, so a service can read only its own `secret/<name>-svc`. On Azure the simpler choice is **Azure Key Vault**, with each container's managed identity given read access to its own secrets.

## MongoDB: one database per service

`mongodb/init/01-service-users.js` runs the first time MongoDB starts with an empty volume. For each name in `SERVICE_DATABASES` it creates the database `<name>` and the user `<name>-svc` with `readWrite` on that database only, so a service can't read another service's data (`docs/CSHARP_SETUP_TODO.md` section 1).

A service's connection string inside the stack:

```
mongodb://booking-svc:<BOOKING_DB_PASSWORD>@mongodb:27017/booking?authSource=booking
```

**MongoDB 8 won't start on Linux kernels 6.19 to 7.0.13** ("MongoDB cannot start: Linux kernel versions 6.19.0 through 7.0.13 have a known incompatibility", MongoDB SERVER-121912). Docker Desktop versions with such a kernel hit this. Check with `docker info --format '{{.KernelVersion}}'`. Update Docker Desktop, or until then add `MONGO_IMAGE=mongo:7.0.48` to your own `.env`.

## The gateway

`nginx/default.conf.template` is rendered by the nginx image's `envsubst` at start, so the upstream addresses are environment variables (`BOOKING_URL`, `MEMBERSHIP_URL`, `IDENTITY_URL`, `GRAFANA_URL` in `services.yml`). Azure mounts the same file with `http://127.0.0.1:<port>` addresses instead (#10).

- `/api/<context>/<path>` reaches the service as `/<path>`, e.g. `/api/booking/reservations/42` → `http://booking-svc:8080/reservations/42`.
- A route whose service isn't running answers 502; the gateway itself still starts.
- The gateway passes on the caller's `X-Correlation-ID`, or sets a new one, and returns it in the response (section 7).
- `/grafana/` reaches Grafana (`GRAFANA_URL`) with the path unchanged, since Grafana serves itself under `/grafana/`. WebSocket upgrades are passed on (Grafana Live).

## Adding a service

1. Add the service to `services.yml`: name it `<name>-svc`, listen on 8080, give it a `healthcheck` on `/health`, and pass its MongoDB and RabbitMQ hosts, the Vault address and `VAULT_DEV_ROOT_TOKEN` as environment variables. Its passwords come from Vault, at `secret/<name>-svc`.
2. If it has a database: add its name to `SERVICE_DATABASES` in `infrastructure.yml`, add `<NAME>_DB_PASSWORD` there (under `x-service-secrets`) and to `.env.example`, then `docker compose down -v` so the init script runs again. Vault gets the new secret on its next start.
3. If it has an HTTP API: add a `location /api/<context>/` block to the Nginx template and the `<NAME>_URL` variable to the gateway in `services.yml`.
4. Logging: pass `LOKI_URL: http://loki:3100` to the service, log with the labels and fields below, and if its name isn't one of the four already in `grafana/provisioning/alerting/error-rate.yml`, add an error-rate rule for it there.

## Logging and monitoring

Services log with NLog to Loki; Grafana shows the logs and raises alerts (`docs/CSHARP_SETUP_TODO.md` section 7). Inside the stack a service pushes to `http://loki:3100`; a service started with `dotnet run` pushes to `http://localhost:3100` (`LOKI_HOST_PORT`).

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
