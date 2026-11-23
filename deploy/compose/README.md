# Local Docker Compose stack

The infrastructure for a laptop and for CI. Services, Newman and the Azure deploy (#10) share this setup.

| Group | File | Contains |
|---|---|---|
| infrastructure | `infrastructure.yml` | MongoDB (one database and one user per service), RabbitMQ with the management UI |
| services | `services.yml` | The Nginx gateway; services and UIs join here as they are built (#18, #8) |
| monitoring | `monitoring.yml` | Loki and Grafana (#6, not yet) |

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
| MongoDB | `mongodb://localhost:27018` (27018, so it doesn't clash with a local MongoDB on 27017) |

Stop, keeping the data:

```bash
docker compose down
```

Stop and delete the data (MongoDB then creates the users again on the next start):

```bash
docker compose down -v
```

One group on its own: `docker compose -f infrastructure.yml up -d --wait`.

## Passwords

`.env` (git-ignored) holds every password; the compose files hold none. `.env.example` has made-up values, which CI uses as they are. Change them in your own `.env`. In Azure the passwords are secure Bicep parameters instead (section 9a).

## MongoDB: one database per service

`mongodb/init/01-service-users.js` runs the first time MongoDB starts with an empty volume. For each name in `SERVICE_DATABASES` it creates the database `<name>` and the user `<name>-svc` with `readWrite` on that database only, so a service can't read another service's data (`docs/CSHARP_SETUP_TODO.md` section 1).

A service's connection string inside the stack:

```
mongodb://booking-svc:<BOOKING_DB_PASSWORD>@mongodb:27017/booking?authSource=booking
```

**MongoDB 8 won't start on Linux kernels 6.19 to 7.0.13** ("MongoDB cannot start: Linux kernel versions 6.19.0 through 7.0.13 have a known incompatibility", MongoDB SERVER-121912). Docker Desktop versions with such a kernel hit this. Check with `docker info --format '{{.KernelVersion}}'`. Update Docker Desktop, or until then add `MONGO_IMAGE=mongo:7.0.48` to your own `.env`.

## The gateway

`nginx/default.conf.template` is rendered by the nginx image's `envsubst` at start, so the upstream addresses are environment variables (`BOOKING_URL`, `MEMBERSHIP_URL`, `IDENTITY_URL` in `services.yml`). Azure mounts the same file with `http://127.0.0.1:<port>` addresses instead (#10).

- `/api/<context>/<path>` reaches the service as `/<path>`, e.g. `/api/booking/reservations/42` → `http://booking-svc:8080/reservations/42`.
- A route whose service isn't running answers 502; the gateway itself still starts.
- The gateway passes on the caller's `X-Correlation-ID`, or sets a new one, and returns it in the response (section 7).

## Adding a service

1. Add the service to `services.yml`: name it `<name>-svc`, listen on 8080, give it a `healthcheck` on `/health`, and pass its MongoDB and RabbitMQ settings as environment variables, with passwords from `.env`.
2. If it has a database: add its name to `SERVICE_DATABASES` in `infrastructure.yml`, add `<NAME>_DB_PASSWORD` there and to `.env.example`, then `docker compose down -v` so the init script runs again.
3. If it has an HTTP API: add a `location /api/<context>/` block to the Nginx template and the `<NAME>_URL` variable to the gateway in `services.yml`.
