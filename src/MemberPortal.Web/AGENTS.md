# MemberPortal.Web: notes for agents

Repo-wide rules: root `AGENTS.md`, `docs/METHODOLOGY.md` and `docs/PR_RULES.md`. This file says where the member portal's facts live.

## What it owns

Nothing: it is a channel, not a bounded context, and it holds no business rule ([`CONTEXT-MAP`](../../docs/domain/CONTEXT-MAP.md) section 3). It is a Blazor UI that calls the services through the gateway, and the services own the data and rules behind each page.

## Commands

- Unit tests: `dotnet test tests/MemberPortal.Web.Tests -c Release`.
- Integration tests: `dotnet test tests/MemberPortal.Web.IntegrationTests -c Release` (needs Docker).
- Run in the stack, from `deploy/compose/`: `docker compose up -d --wait member-portal`; the gateway sends it every path that isn't `/api/`, `/health` or `/grafana/`, so it is http://localhost:8080/.
- Compose name: `member-portal` (also Loki's `app` label). With `dotnet run` it reaches the stack's APIs through `Gateway:BaseUrl` in `appsettings.Development.json`.

## Contracts and access

- OpenAPI: none; it serves pages, not an API.
- Who may open what: the `MemberPortal.Web` rows in [`docs/domain/ACCESS.md`](../../docs/domain/ACCESS.md).
- Events: none published or consumed.
- Postman: [`tests/postman/smoke/thin-slice.postman_collection.json`](../../tests/postman/smoke/thin-slice.postman_collection.json) checks the home page after a deploy.

## Change here also touches

- New or changed page → an `ACCESS.md` row, the page's tests in `tests/MemberPortal.Web.Tests/` (bUnit), and the service endpoint behind it: that service's OpenAPI, `ACCESS.md` rows, Postman collection and `CONTEXT.md`.
- A service's response shape → its OpenAPI contract and the portal's clients, which call it through the gateway.
- New term → `GLOSSARY.md`. There is no `CONTEXT.md` here: the portal owns no model.
