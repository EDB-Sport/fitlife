# Booking (`src/BookingService/`)

> From `docs/templates/CONTEXT.md`. Keep it under about 80 lines, and update it in the same PR that changes the model.
> **Status: invariants 1–4 modelled (#155–#158).** The context map (`docs/domain/CONTEXT-MAP.md`) is decided in ADR 007. The tactical model below is DDD_PLAN step 4; the rules come from the TO-BE-kontekst §8 (Booking). Each further invariant is its own issue.

## Purpose

Class scheduling and booking, a core subdomain: one class schedule for all six centres, where members reserve a seat, cancel, or join a waitlist (context map).

## Language in this context

The rows are in `GLOSSARY.md`; this is the part Booking uses.

| Term | Meaning here | In code |
|---|---|---|
| Hold | One scheduled run of a class, with a capacity | `ClassSession` |
| Kapacitet | How many members can have a reservation on one class session | `Capacity` |
| Booking | A member's seat on one class session | `Reservation` |
| Ventelisteplads | A member's place in the queue for a full class session | `WaitlistEntry` |
| Medlem | Booking only knows the member's id | `MemberId` (a `Guid`) |
| Booking oprettet | A member got a seat, directly or from the waitlist | `ReservationMade` |
| Booking afmeldt | A member gave up their seat | `ReservationCancelled` |

## Aggregates

**`ClassSession`** (root), in `Domain/`. Reservations and waitlist entries change only through its methods, so the root can guard every rule below. No public setters (METHODOLOGY §11.2 rule 4).

| Part | Kind | Notes |
|---|---|---|
| `ClassSession` | Aggregate root | Id, `Capacity`, the reservations, the waitlist |
| `Reservation` | Entity, inside the aggregate | Made only by `ClassSession.Reserve` |
| `Capacity` | Value object | At least 1 seat |
| `WaitlistEntry` | Entity, inside the aggregate | Made only by `ClassSession.JoinWaitlist`; becomes a `Reservation` when a seat is freed |

### Invariants

| # | Rule (TO-BE §8) | Test or issue |
|---|---|---|
| 1 | The reservations never exceed the capacity | `ClassSessionTests.Reserve_WhenClassSessionIsFull_IsRejected` |
| 2 | A member can't reserve the same class session twice | `ClassSessionTests.Reserve_WhenTheMemberAlreadyHasAReservation_IsRejected` |
| 3 | A full class session offers a waitlist entry, first come first served | `ClassSessionTests.JoinWaitlist_WhenClassSessionIsFull_AddsWaitlistEntriesInTheOrderTheyWereMade`, `ClassSessionTests.JoinWaitlist_WithFreeSeats_IsRejected` |
| 4 | A freed seat goes to the first waitlist entry | `ClassSessionTests.CancelReservation_OnAFullClassSession_GivesTheSeatToTheFirstWaitlistEntry`, `ClassSessionTests.CancelReservation_WithAnEmptyWaitlist_FreesTheSeat` |
| 5 | Class sessions can be reserved at most 4 weeks ahead | #159 |
| 6 | The waitlist closes 2 hours before start | #160 |
| 7 | A cancellation under 2 hours before start is a late cancellation | #161 |
| 8 | A cancelled class session cancels every reservation and waitlist entry | #162 |

## Commands, queries and events

| Kind | Name | Notes |
|---|---|---|
| Command | `ClassSession.Reserve(memberId)` | Returns `ReservationResult.Made`, `MemberAlreadyHasReservation` or `ClassSessionFull` |
| Command | `ClassSession.JoinWaitlist(memberId)` | Returns `WaitlistResult.Joined` or `ClassSessionNotFull` |
| Command | `ClassSession.CancelReservation(memberId)` | Returns `CancellationResult.Cancelled` or `NoReservation` |
| Event | `ReservationCancelled` | Raised when a member gives up their seat |
| Event | `ReservationMade` | Raised when the first waitlist entry gets the freed seat. Published later (#171 and after); the events wait in `ClassSession.DomainEvents` |

## Talks to

None yet. The relations (Membership upstream, Notification downstream) are in the context map.

| Other context / system | How | Pattern | Why |
|---|---|---|---|

## Rules for this folder

- Other services are reached over HTTP or RabbitMQ events only, never by a `ProjectReference` (FITLIFE001).
- `/health` stays mapped. `docker compose up --wait` and the Azure probes use it.
- The OpenAPI document and Scalar are mapped in Development only (course rule #7). `ApiDocumentationTests` checks it.
