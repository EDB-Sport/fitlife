# MembershipService: notes for agents

Repo-wide rules: root `AGENTS.md`, `docs/METHODOLOGY.md` and `docs/PR_RULES.md`. This file says where Membership's facts live.

## What it owns

The Membership context (supporting subdomain): members, memberships and membership types. The model and its terms: [`CONTEXT.md`](CONTEXT.md). Its place in the map: [`docs/domain/CONTEXT-MAP.md`](../../docs/domain/CONTEXT-MAP.md) sections 3 and 4.

## Commands

- Unit tests: `dotnet test tests/MembershipService.Tests -c Release`.
- Integration tests: `dotnet test tests/MembershipService.IntegrationTests -c Release` (needs Docker, for Testcontainers).
- Run in the stack, from `deploy/compose/`: `docker compose up -d --wait membership-svc`; the gateway serves it at http://localhost:8080/api/membership/.
- Compose name: `membership-svc` (also Loki's `app` label and Vault's `secret/membership-svc`).

## Contracts and access

- OpenAPI: [`contracts/openapi/membershipservice.json`](../../contracts/openapi/membershipservice.json). `dotnet build -c Release` regenerates it; commit it (`tools/ci/check-contracts.sh`).
- Who may call what: the `MembershipService` rows in [`docs/domain/ACCESS.md`](../../docs/domain/ACCESS.md).
- Events: none published or consumed yet. Planned in [`CONTEXT-MAP`](../../docs/domain/CONTEXT-MAP.md) section 5: publishes `MembershipPaused`, `MembershipCancelled` and `PaymentFailed`.
- Postman: [`tests/postman/services/`](../../tests/postman/services/).

## Change here also touches

- New or changed endpoint → regenerate `contracts/openapi/membershipservice.json`, add or change an `ACCESS.md` row, add or update the Postman collection, update `CONTEXT.md`.
- `GET /members/by-account/{accountId}` is read by Booking (`src/BookingService/Clients/`); its answer also changes this service's callers.
- New or changed event → a row in `docs/domain/CONTEXT-MAP.md` section 5, `contracts/events/<event>.schema.json` and `contracts/events/README.md`, the integration event in [`libs/FitLife.Messaging/Events/`](../../libs/FitLife.Messaging/Events/), the publisher mapping, the consumer and its test, and both contexts' `CONTEXT.md`.
- Payment provider fields → `src/MembershipService/PaymentProvider/PaymentProviderClient.cs` and [`tests/postman/mocks/payment-provider.mock.postman_collection.json`](../../tests/postman/mocks/payment-provider.mock.postman_collection.json).
- New term → `GLOSSARY.md` and `CONTEXT.md`.
