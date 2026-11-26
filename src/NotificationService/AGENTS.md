# NotificationService: notes for agents

Repo-wide rules: root `AGENTS.md`, `docs/METHODOLOGY.md` and `docs/PR_RULES.md`. This file says where Notification's facts live.

## What it owns

The Notification context (supporting subdomain): notifications to members about what happened in Booking and Membership. The model and its terms: [`CONTEXT.md`](CONTEXT.md). Its place in the map: [`docs/domain/CONTEXT-MAP.md`](../../docs/domain/CONTEXT-MAP.md) sections 3 and 5.

## Commands

- Unit tests: `dotnet test tests/NotificationService.Tests -c Release`.
- Integration tests: `dotnet test tests/NotificationService.IntegrationTests -c Release` (needs Docker, for Testcontainers and RabbitMQ).
- Run in the stack, from `deploy/compose/`: `docker compose up -d --wait notification-svc`. It has no gateway route; nothing calls it over HTTP, and `/health` is the only endpoint.
- Compose name: `notification-svc` (also Loki's `app` label and Vault's `secret/notification-svc`).

## Contracts and access

- OpenAPI: [`contracts/openapi/notificationservice.json`](../../contracts/openapi/notificationservice.json) (`/health` only today). `dotnet build -c Release` regenerates it; commit it (`tools/ci/check-contracts.sh`).
- Who may call what: the `NotificationService` row in [`docs/domain/ACCESS.md`](../../docs/domain/ACCESS.md) (planned endpoint).
- Events: consumes `ReservationMade` on queue `notification.reservation-made` (schema [`contracts/events/reservation-made.schema.json`](../../contracts/events/reservation-made.schema.json)); publishes none.
- Postman: [`tests/postman/services/`](../../tests/postman/services/).

## Change here also touches

- New or changed event consumer → a row in `docs/domain/CONTEXT-MAP.md` section 5, the schema in `contracts/events/`, the integration event in [`libs/FitLife.Messaging/Events/`](../../libs/FitLife.Messaging/Events/), the subscriber with its integration test, and both contexts' `CONTEXT.md`.
- New or changed endpoint → regenerate `contracts/openapi/notificationservice.json`, add or change an `ACCESS.md` row, add or update the Postman collection, update `CONTEXT.md`.
- Channels (SMS, e-mail, push) are external and only logged in the PoC; their formats stay outside this context.
- New term → `GLOSSARY.md` and `CONTEXT.md`.
