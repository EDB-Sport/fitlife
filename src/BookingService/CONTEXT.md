# Booking (`src/BookingService/`)

> From `docs/templates/CONTEXT.md`. Keep it under about 80 lines, and update it in the same PR that changes the model.
> **Status: skeleton.** The context map (`docs/domain/CONTEXT-MAP.md`, issue #2) is still a proposal, and which context the PoC builds first is decided in #18. Nothing below is modelled yet. Fill in each section when the first issue for this context adds the model (step 4 of `docs/DDD_PLAN.md`).

## Purpose

Class scheduling and booking, a core subdomain: one class schedule for all six centres, where members reserve a seat, cancel, or join a waitlist (context map, proposed).

## Language in this context

None yet. Terms go into `GLOSSARY.md` and here in the PR that introduces them.

| Term | Meaning here | In code |
|---|---|---|

## Aggregates

None yet.

## Commands, queries and events

None yet.

| Kind | Name | Notes |
|---|---|---|

## Talks to

None yet. The proposed relations (Membership upstream, Notification downstream) are in the context map.

| Other context / system | How | Pattern | Why |
|---|---|---|---|

## Rules for this folder

- Other services are reached over HTTP or RabbitMQ events only, never by a `ProjectReference` (FITLIFE001).
- `/health` stays mapped. `docker compose up --wait` and the Azure probes use it.
- The OpenAPI document and Scalar are mapped in Development only (course rule #7). `ApiDocumentationTests` checks it.
