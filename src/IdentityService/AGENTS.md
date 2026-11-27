# IdentityService: notes for agents

Repo-wide rules: root `AGENTS.md`, `docs/METHODOLOGY.md` and `docs/PR_RULES.md`. This file says where Identity's facts live.

## What it owns

The Identity context: accounts, roles and the tokens they are carried in (generic subdomain "Identity and access"). The model and its terms: [`CONTEXT.md`](CONTEXT.md). Its place in the map: [`docs/domain/CONTEXT-MAP.md`](../../docs/domain/CONTEXT-MAP.md) sections 3 and 4.

## Commands

- Unit tests: `dotnet test tests/IdentityService.Tests -c Release`.
- Integration tests: `dotnet test tests/IdentityService.IntegrationTests -c Release` (needs Docker, for Testcontainers).
- Run in the stack, from `deploy/compose/`: `docker compose up -d --wait identity-svc`; the gateway serves it at http://localhost:8080/api/identity/.
- Compose name: `identity-svc` (also Loki's `app` label and Vault's `secret/identity-svc`).

## Contracts and access

- OpenAPI: [`contracts/openapi/identityservice.json`](../../contracts/openapi/identityservice.json). `dotnet build -c Release` regenerates it; commit it (`tools/ci/check-contracts.sh`).
- Who may call what: the `IdentityService` rows in [`docs/domain/ACCESS.md`](../../docs/domain/ACCESS.md).
- Events: none published or consumed.
- Postman: [`tests/postman/services/identity.postman_collection.json`](../../tests/postman/services/identity.postman_collection.json).

## Change here also touches

- New or changed endpoint → regenerate `contracts/openapi/identityservice.json`, add or change an `ACCESS.md` row, update the Postman collection, update `CONTEXT.md`.
- Token claims or roles → [`libs/FitLife.Authentication/`](../../libs/FitLife.Authentication/), the callers of `AddFitLifeJwtAuthentication()` in `src/BookingService/Program.cs`, `src/IdentityService/Program.cs`, `src/MembershipService/Program.cs` and `src/NotificationService/Program.cs` (`git grep -n AddFitLifeJwtAuthentication src/` finds them; McpServer and MemberPortal.Web don't call it), and [ADR 009](../../docs/adr/009-log-in-and-roles.md).
- The demo accounts → `Seed:DemoAccounts` and the `DEMO_*_PASSWORD` values in [`deploy/compose/services.yml`](../../deploy/compose/services.yml) and `deploy/compose/.env.example`.
- New term → `GLOSSARY.md` and `CONTEXT.md`.
