# Booking (`src/BookingService/`)

> From `docs/templates/CONTEXT.md`. Keep it under about 80 lines, and update it in the same PR that changes the model.
> **Status: invariants 1–8 modelled (#155–#162).** The context map (`docs/domain/CONTEXT-MAP.md`) is decided in ADR 007. The tactical model below is DDD_PLAN step 4; the rules come from the TO-BE-kontekst §8 (Booking). Each further invariant is its own issue.

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
| Sen afmelding | A cancellation under 2 hours before start, statistics only. A seat from the waitlist is never late | `ReservationCancelled.IsLateCancellation` |
| Ventelisteplads udløbet | The waitlist closed and a member's waitlist entry was removed without a seat | `WaitlistEntryExpired` |
| Hold aflyst | A class session will not take place | `ClassSessionCancelled` |

## Aggregates

**`ClassSession`** (root), in `Domain/`. Reservations and waitlist entries change only through its methods, so the root can guard every rule below. No public setters (METHODOLOGY §11.2 rule 4).

| Part | Kind | Notes |
|---|---|---|
| `ClassSession` | Aggregate root | Id, `Capacity`, start time, whether it is cancelled, the reservations, the waitlist |
| `Reservation` | Entity, inside the aggregate | Made by `ClassSession.Reserve`, or from the waitlist (`IsFromWaitlist`) |
| `Capacity` | Value object | At least 1 seat |
| `WaitlistEntry` | Entity, inside the aggregate | Made only by `ClassSession.JoinWaitlist`; becomes a `Reservation` when a seat is freed |

### Invariants

| # | Rule (TO-BE §8) | Test or issue |
|---|---|---|
| 1 | The reservations never exceed the capacity | `ClassSessionTests.Reserve_WhenClassSessionIsFull_IsRejected` |
| 2 | A member can't reserve the same class session twice, and has at most one place on it: a seat or a waitlist entry | `ClassSessionTests.Reserve_WhenTheMemberAlreadyHasAReservation_IsRejected`, `ClassSessionTests.JoinWaitlist_WhenTheMemberAlreadyHasAReservation_IsRejected`, `ClassSessionTests.JoinWaitlist_WhenTheMemberIsAlreadyOnTheWaitlist_IsRejected` |
| 3 | A full class session offers a waitlist entry, first come first served | `ClassSessionTests.JoinWaitlist_WhenClassSessionIsFull_AddsWaitlistEntriesInTheOrderTheyWereMade`, `ClassSessionTests.JoinWaitlist_WithFreeSeats_IsRejected` |
| 4 | A freed seat goes to the first waitlist entry | `ClassSessionTests.CancelReservation_OnAFullClassSession_GivesTheSeatToTheFirstWaitlistEntry`, `ClassSessionTests.CancelReservation_WithAnEmptyWaitlist_FreesTheSeat` |
| 5 | Class sessions can be reserved at most 4 weeks ahead | `ClassSessionTests.Reserve_MoreThanFourWeeksAhead_IsRejected`, `ClassSessionTests.Reserve_ExactlyFourWeeksAhead_IsReserved` |
| 6 | The waitlist closes 2 hours before start | `ClassSessionTests.JoinWaitlist_LessThanTwoHoursBeforeStart_IsRejected`, `ClassSessionTests.CloseWaitlist_TwoHoursBeforeStart_RemovesTheWaitlistEntriesAndTellsEachMember`, `ClassSessionTests.CloseWaitlist_MoreThanTwoHoursBeforeStart_IsRejected` |
| 7 | A cancellation under 2 hours before start is a late cancellation | `ClassSessionTests.CancelReservation_LessThanTwoHoursBeforeStart_IsALateCancellation`, `ClassSessionTests.CancelReservation_OfASeatFromTheWaitlist_IsNeverALateCancellation` |
| 8 | A cancelled class session cancels every reservation and waitlist entry | `ClassSessionCancelTests.Cancel_RemovesEveryReservationAndWaitlistEntryAndNamesTheirMembers`, `ClassSessionCancelTests.Reserve_OnACancelledClassSession_IsRejected`, `ClassSessionCancelTests.Cancel_OnACancelledClassSession_RaisesNoSecondClassSessionCancelled`, `ClassSessionCancelTests.JoinWaitlist_OnACancelledClassSession_IsRejected` |

## Commands, queries and events

| Kind | Name | Notes |
|---|---|---|
| Command | `ClassSession.Reserve(memberId, clock)` | Returns `ReservationResult.Made`, `ClassSessionCancelled`, `MemberAlreadyHasReservation`, `TooFarAhead` or `ClassSessionFull`. The clock is a `TimeProvider` |
| Command | `ClassSession.JoinWaitlist(memberId, clock)` | Returns `WaitlistResult.Joined`, `ClassSessionCancelled`, `MemberAlreadyHasReservation`, `MemberAlreadyOnWaitlist`, `ClassSessionNotFull` or `WaitlistClosed` |
| Command | `ClassSession.CancelReservation(memberId, clock)` | Returns `CancellationResult.Cancelled` or `NoReservation` |
| Command | `ClassSession.CloseWaitlist(clock)` | Returns `WaitlistClosingResult.Closed` or `TooEarly`. Nothing calls it yet: a scheduled job does, in a later issue. Until it has run, `CancelReservation` still gives a freed seat to the first waitlist entry, also in the last 2 hours |
| Command | `ClassSession.Cancel()` | Empties the reservations and the waitlist; `IsCancelled` becomes true. On a cancelled class session it changes nothing and raises no second `ClassSessionCancelled` |
| Event | `ReservationCancelled` | Raised when a member gives up their seat. `IsLateCancellation` says whether it was a late cancellation |
| Event | `ReservationMade` | Raised when the first waitlist entry gets the freed seat. Published later (#171 and after); the events wait in `ClassSession.DomainEvents` |
| Event | `WaitlistEntryExpired` | Raised for each waitlist entry removed when the waitlist closes |
| Event | `ClassSessionCancelled` | Raised when a class session is cancelled, with the member ids of every reservation and waitlist entry |

## Talks to

The relations (Membership upstream, Notification downstream) are in the context map.

| Other context / system | How | Pattern | Why |
|---|---|---|---|
| Membership | REST: `GET members/by-account/{accountId}` with the member's own token passed on (`Clients/MembershipClient`, `IMembershipClient`). Address `Membership:BaseUrl`; time-out, retry and circuit breaker from `AddStandardResilienceHandler()` | Open Host Service (Membership upstream) | Who the member behind an account is, and whether the membership is active, before a reservation (#256, used by #260). The answer is found, not a member, or unavailable, never an exception |

## Rules for this folder

- Other services are reached over HTTP or RabbitMQ events only, never by a `ProjectReference` (FITLIFE001).
- `/health` stays mapped. `docker compose up --wait` and the Azure probes use it.
- The OpenAPI document and Scalar are mapped in Development only (course rule #7). `ApiDocumentationTests` checks it.
