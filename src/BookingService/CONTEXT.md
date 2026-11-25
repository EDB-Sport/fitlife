# Booking (`src/BookingService/`)

> From `docs/templates/CONTEXT.md`. Keep it under about 80 lines, and update it in the same PR that changes the model.
> **Status: first invariant modelled (#155).** The context map (`docs/domain/CONTEXT-MAP.md`) is decided in ADR 007. The tactical model below is DDD_PLAN step 4; the rules come from the TO-BE-kontekst §8 (Booking). Each further invariant is its own issue.

## Purpose

Class scheduling and booking, a core subdomain: one class schedule for all six centres, where members reserve a seat, cancel, or join a waitlist (context map).

## Language in this context

The rows are in `GLOSSARY.md`; this is the part Booking uses.

| Term | Meaning here | In code |
|---|---|---|
| Hold | One scheduled run of a class, with a capacity | `ClassSession` |
| Kapacitet | How many members can have a reservation on one class session | `Capacity` |
| Booking | A member's seat on one class session | `Reservation` |
| Ventelisteplads | A member's place in the queue for a full class session | `WaitlistEntry` (#157) |
| Medlem | Booking only knows the member's id | `MemberId` (a `Guid`) |

## Aggregates

**`ClassSession`** (root), in `Domain/`. Reservations and waitlist entries change only through its methods, so the root can guard every rule below. No public setters (METHODOLOGY §11.2 rule 4).

| Part | Kind | Notes |
|---|---|---|
| `ClassSession` | Aggregate root | Id, `Capacity`, the reservations |
| `Reservation` | Entity, inside the aggregate | Made only by `ClassSession.Reserve` |
| `Capacity` | Value object | At least 1 seat |
| `WaitlistEntry` | Entity, inside the aggregate | #157 |

### Invariants

| # | Rule (TO-BE §8) | Test or issue |
|---|---|---|
| 1 | The reservations never exceed the capacity | `ClassSessionTests.Reserve_WhenClassSessionIsFull_IsRejected` |
| 2 | A member can't reserve the same class session twice | #156 |
| 3 | A full class session offers a waitlist entry, first come first served | #157 |
| 4 | A freed seat goes to the first waitlist entry | #158 |
| 5 | Class sessions can be reserved at most 4 weeks ahead | #159 |
| 6 | The waitlist closes 2 hours before start | #160 |
| 7 | A cancellation under 2 hours before start is a late cancellation | #161 |
| 8 | A cancelled class session cancels every reservation and waitlist entry | #162 |

## Commands, queries and events

| Kind | Name | Notes |
|---|---|---|
| Command | `ClassSession.Reserve(memberId)` | Returns `ReservationResult.Made` or `ClassSessionFull` |

## Talks to

The relations (Membership upstream, Notification downstream) are in the context map.

| Other context / system | How | Pattern | Why |
|---|---|---|---|
| Membership | REST: `GET members/by-account/{accountId}` with the member's own token passed on (`Clients/MembershipClient`, `IMembershipClient`). Address `Membership:BaseUrl`; time-out, retry and circuit breaker from `AddStandardResilienceHandler()` | Open Host Service (Membership upstream) | Who the member behind an account is, and whether the membership is active, before a reservation (#256, used by #260). The answer is found, not a member, or unavailable, never an exception |

## Rules for this folder

- Other services are reached over HTTP or RabbitMQ events only, never by a `ProjectReference` (FITLIFE001).
- `/health` stays mapped. `docker compose up --wait` and the Azure probes use it.
- The OpenAPI document and Scalar are mapped in Development only (course rule #7). `ApiDocumentationTests` checks it.
