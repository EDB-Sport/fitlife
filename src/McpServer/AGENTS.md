# McpServer: notes for agents

Repo-wide rules: root `AGENTS.md`, `docs/METHODOLOGY.md` and `docs/PR_RULES.md`. This file says where the MCP server's facts live.

## What it owns

Nothing: it is a channel, not a bounded context, and it holds no business rule or database ([`CONTEXT-MAP`](../../docs/domain/CONTEXT-MAP.md) section 3, [ADR 008](../../docs/adr/008-mcp-server-in-the-poc.md)). It lets a member's AI assistant call the services through the gateway, as the member portal does. Its own contract is the tool list, [`contracts/mcp/tools.json`](../../contracts/mcp/tools.json).

## Commands

- Unit tests: `dotnet test tests/McpServer.Tests -c Release`.
- Integration tests: `dotnet test tests/McpServer.IntegrationTests -c Release` (needs Docker).
- Run in the stack, from `deploy/compose/`: `docker compose up -d --wait mcp-server`; the gateway serves it at http://localhost:8080/mcp.
- Compose name: `mcp-server` (also Loki's `app` label). It reaches Booking through `Gateway__BaseUrl`.

## Contracts and access

- OpenAPI: none; the tool list is the contract. A tool call goes through a service's OpenAPI contract.
- Who may call what: the `McpServer` rows in [`docs/domain/ACCESS.md`](../../docs/domain/ACCESS.md).
- Events: none published or consumed.
- Postman: [`tests/postman/services/`](../../tests/postman/services/).

## Change here also touches

- New or changed tool → `contracts/mcp/tools.json`, an `ACCESS.md` row, the guardrails in [ADR 008](../../docs/adr/008-mcp-server-in-the-poc.md), and tests in `tests/McpServer.Tests/`.
- The service behind a tool → that service's OpenAPI, `ACCESS.md` rows, Postman collection and `CONTEXT.md`; the tool only passes the member's token through.
- New term → `GLOSSARY.md`. There is no `CONTEXT.md` here: the server owns no model.
