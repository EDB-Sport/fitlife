# BookingService: notes for agents

Repo-wide rules: root `AGENTS.md`, `docs/METHODOLOGY.md` and `docs/PR_RULES.md`. This file says where Booking's facts live.

## What it owns

The Booking context (core subdomain): centres, class types, class sessions, reservations and waitlist entries. The model and its terms: [`CONTEXT.md`](CONTEXT.md). Its place in the map: [`docs/domain/CONTEXT-MAP.md`](../../docs/domain/CONTEXT-MAP.md) sections 3, 4 and 5.

## Commands

- Unit tests: `dotnet test tests/BookingService.Tests -c Release`.
- Integration tests: `dotnet test tests/BookingService.IntegrationTests -c Release` (needs Docker, for Testcontainers).
- Run in the stack, from `deploy/compose/`: `docker compose up -d --wait booking-svc`; the gateway serves it at http://localhost:8080/api/booking/.
- Compose name: `booking-svc` (also Loki's `app` label and Vault's `secret/booking-svc`).

## Contracts and access

- OpenAPI: [`contracts/openapi/bookingservice.json`](../../contracts/openapi/bookingservice.json). `dotnet build -c Release` regenerates it; commit it (`tools/ci/check-contracts.sh`).
- Who may call what: the `BookingService` rows in [`docs/domain/ACCESS.md`](../../docs/domain/ACCESS.md).
- Events: raises `ReservationMade`, `ReservationCancelled`, `WaitlistEntryExpired`, `ClassSessionCancelled` and `ClassSessionChanged` in `ClassSession.DomainEvents` ([`CONTEXT.md`](CONTEXT.md)); none is published yet. Four cross to Notification (`CONTEXT-MAP` section 5); only `ReservationMade` has a schema in `contracts/events/` so far ([`reservation-made.schema.json`](../../contracts/events/reservation-made.schema.json)). `WaitlistEntryExpired` stays inside Booking. Consumes none yet; section 5 plans `MembershipPaused` and `MembershipCancelled`, on which Booking cancels reservations.
- Postman: [`tests/postman/services/`](../../tests/postman/services/).

## Change here also touches

- New or changed endpoint → regenerate `contracts/openapi/bookingservice.json`, add or change an `ACCESS.md` row, add or update the Postman collection, update `CONTEXT.md`.
- New or changed event → a row in `docs/domain/CONTEXT-MAP.md` section 5, `contracts/events/<event>.schema.json` and `contracts/events/README.md`, the integration event in [`libs/FitLife.Messaging/Events/`](../../libs/FitLife.Messaging/Events/), the publisher mapping, the consumer's subscriber and test, and both contexts' `CONTEXT.md`.
- Membership's `GET /members/by-account/{accountId}` answer is read in `src/BookingService/Clients/`; a change there also changes this service.
- New term → `GLOSSARY.md` and `CONTEXT.md`.
