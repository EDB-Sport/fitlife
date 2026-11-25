# ADR 008: The PoC shows the MCP server

- **Status:** accepted
- **Date:** 2026-11-25
- **Deciders:** Mohamed (proposal), and the reviewers who sign off the PR (issue #212)

## Context

The MCP server lets a member's AI assistant use our API through the Model Context Protocol. ADR 007 made it a channel, not a bounded context, and left open whether the PoC shows it: "the PoC may show it in its own issue" (ADR 007, Q4). This ADR answers Q4.

Forces:
- The case's competitors offer "personlige programmer, live-streamede hold og AI-baseret coaching" [PB p. 4]. A member's own AI assistant that can see the class schedule and book for them is FitLife's answer on the booking side, without FitLife building an AI of its own.
- The PoC must show that the team "kan levere services som kan anvendes af fremtidige samarbejdspartnere i form af veldefinerede servicekontrakter" [PB p. 10]. An MCP server is a third-party client of our contracts: its tool list is a contract of its own, and every tool call goes through a service's OpenAPI contract.
- The TO-BE context document (version 2026-11-23) puts the MCP server in the PoC "fordi den demonstrerer servicekontrakter til tredjepart (krav i projektbeskrivelsen) og er en differentierende kanal" (section 1). Section 10 lists it under Must, with its tools and guardrails. The context map still said LATER, so the two disagreed.
- Principle PA3: all channels use the same services, and the MCP server has no business logic. Principle PS2: actions from AI assistants are logged.
- MCP has nothing to do with wearables (ADR 007, Q9). Wearables stay out of the PoC.

## Decision

The PoC shows the MCP server as a channel in `src/McpServer/`, with no business rule and no database of its own (PA3). It calls the services through the API gateway, as the member portal does. It is small, and these guardrails are fixed:

- **Tools:** only the class schedule and the member's own reservations and waitlist entries. Reading the schedule needs no token, as in the member portal.
- **The member's own token:** the server passes on the caller's token unchanged and stores none. No tool takes a member id or an account id as input, so a token can only act for its own member.
- **A confirmation step:** a tool that changes something (reserve, cancel, join or leave a waitlist) does nothing without an explicit confirmation. Without it, it only says what it would do.
- **A rate limit** per account on `/mcp`.
- **Every tool call is logged** with the account and the tool name, never the token (PS2).
- **No health or payment data:** no tool reads or changes a membership's payment, and none reaches Training.

## Alternatives considered

- **Describe the MCP server in the report only.** Less to build, but the PoC would show no client of our contracts from outside, and the report would describe a requirement [PB p. 10] it doesn't demonstrate. It would also contradict the TO-BE context document, which is the group's source of truth for part 2.
- **A bounded context of its own, with its own data.** Rejected in ADR 007: it has no business rules, and the rules for a reservation belong to Booking. A second copy would drift.
- **A larger MCP server** (membership tools, scopes per tool). Not now: the guardrails above are what the PoC needs to show the pattern safely. Scopes per tool are an open question in #216.

## Consequences

- The MCP server is built in its own issues: the skeleton in the compose stack and its gateway route (#213), the SDK and the read-only `list_class_sessions` (#214), the member tools with the confirmation step (#215), and the rate limit and the call log (#216).
- One new package, the official C# SDK for the Model Context Protocol. Its version is pinned in `Directory.Packages.props` in #214.
- The tool list becomes a contract next to the OpenAPI documents and the event schemas, `contracts/mcp/tools.json`, delivered with the report's contracts.
- The server passes Booking's rejection `code` (for example `ClassSessionFull`) on to the assistant instead of deciding anything itself (#215), so that code is part of what the MCP server relies on in Booking's contract.
- The report can show the same reservation made from the member portal and from an AI assistant, with one set of rules in Booking.
- Context map section 3 now says the PoC shows the MCP server, as the TO-BE context document does.
