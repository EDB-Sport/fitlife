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
| Center | One of the six centres. A class session knows its id; the class schedule shows its name (reference data, read-only) | `CenterId` (a `Guid`), `Models/Center` |
| Holdtype | The kind of class a class session is a run of. A class session knows its id; the class schedule shows its name (reference data, read-only) | `ClassTypeId` (a `Guid`), `Models/ClassType` |
| Instruktør | The trainer who leads a class session. Booking only knows the name | `Instructor` |
| Holdoplysninger | A class session's centre, class type, instructor and duration | `ClassSessionDetails` |
| Booking oprettet | A member got a seat, directly or from the waitlist | `ReservationMade` |
| Booking afmeldt | A member gave up their seat | `ReservationCancelled` |
| Sen afmelding | A cancellation under 2 hours before start, statistics only. A seat from the waitlist is never late | `ReservationCancelled.IsLateCancellation` |
| Ventelisteplads udløbet | The waitlist closed and a member's waitlist entry was removed without a seat | `WaitlistEntryExpired` |
| Hold aflyst | A class session will not take place | `ClassSessionCancelled` |
| Hold ændret | A class session got a new start time. Its reservations and waitlist entries are kept | `ClassSessionChanged` |

## Aggregates

**`ClassSession`** (root), in `Domain/`. Reservations and waitlist entries change only through its methods, so the root can guard every rule below. No public setters (METHODOLOGY §11.2 rule 4).

| Part | Kind | Notes |
|---|---|---|
| `ClassSession` | Aggregate root | Id, `Capacity`, start time, `ClassSessionDetails` (centre id, class type id, instructor, duration), whether it is cancelled, the reservations, the waitlist |
| `Reservation` | Entity, inside the aggregate | Made by `ClassSession.Reserve`, or from the waitlist (`IsFromWaitlist`). `IsFromBeforeAChangedStart` marks a seat reserved before `ChangeStart` (not stored yet: #241) |
| `Capacity` | Value object | At least 1 seat |
| `ClassSessionDetails` | Value object | Centre id, class type id, `Instructor` and a duration of more than zero. No start time: that is `ClassSession.StartTime` |
| `Instructor` | Value object | A name that isn't empty or white space |
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
| 9 | After a changed start, a member who already had a reservation cancels free: never a late cancellation | `ClassSessionChangeStartTests.ChangeStart_ThenAMemberWhoHadAReservationCancelsUnderTwoHoursBeforeStart_IsNotALateCancellation`, `ClassSessionChangeStartTests.ChangeStart_ThenAMemberWhoReservedAfterwardsCancelsUnderTwoHoursBeforeStart_IsALateCancellation` |
| 10 | A class session that has started can't be reserved; a free seat can be taken until the start time (#312's safe default) | `ClassSessionStartedTests.Reserve_AfterTheClassSessionHasStarted_IsRejected`, `ClassSessionStartedTests.Reserve_JustBeforeStart_IsReserved` |

## Commands, queries and events

| Kind | Name | Notes |
|---|---|---|
| Command | `ClassSession.Reserve(memberId, clock)` | Returns `ReservationResult.Made`, `ClassSessionCancelled`, `MemberAlreadyHasReservation`, `ClassSessionStarted`, `TooFarAhead` or `ClassSessionFull`. The clock is a `TimeProvider` |
| Command | `ClassSession.JoinWaitlist(memberId, clock)` | Returns `WaitlistResult.Joined`, `ClassSessionCancelled`, `MemberAlreadyHasReservation`, `MemberAlreadyOnWaitlist`, `ClassSessionNotFull` or `WaitlistClosed` |
| Command | `ClassSession.CancelReservation(memberId, clock)` | Returns `CancellationResult.Cancelled` or `NoReservation` |
| Command | `ClassSession.CloseWaitlist(clock)` | Returns `WaitlistClosingResult.Closed` or `TooEarly`. Nothing calls it yet: a scheduled job does, in a later issue. Until it has run, `CancelReservation` still gives a freed seat to the first waitlist entry, also in the last 2 hours |
| Command | `ClassSession.Cancel()` | Empties the reservations and the waitlist; `IsCancelled` becomes true. On a cancelled class session it changes nothing and raises no second `ClassSessionCancelled` |
| Command | `ClassSession.ChangeStart(newStart, clock)` | Returns `ChangeStartResult.Changed`, `ClassSessionCancelled` or `StartNotInTheFuture`. Keeps the reservations and the waitlist; checks neither the 4 weeks nor the waitlist's closing time (#240). The endpoint is #241 |
| Event | `ReservationCancelled` | Raised when a member gives up their seat. `IsLateCancellation` says whether it was a late cancellation |
| Event | `ReservationMade` | Raised when the first waitlist entry gets the freed seat. Published later (#171 and after); the events wait in `ClassSession.DomainEvents` |
| Event | `WaitlistEntryExpired` | Raised for each waitlist entry removed when the waitlist closes |
| Event | `ClassSessionCancelled` | Raised when a class session is cancelled, with the member ids of every reservation and waitlist entry |
| Event | `ClassSessionChanged` | Raised when `ChangeStart` moves a class session, with the members who have a reservation (not the waitlisted), the previous and the new start. Published in #242 |

### Storage and HTTP (#171)

- **Repository:** `Application/IClassSessionRepository`; its MongoDB class `Repositories/ClassSessionRepository` is the only code that uses `MongoDB.Driver` (TEAM001). One document per class session in the `classSessions` collection, reservations and waitlist inside it. A `Version` field makes a save on a stale copy fail (optimistic concurrency), so two requests at once can't overbook.
- **HTTP:** `Api/ClassSessionsController`. `GET /class-sessions?centerId=`, `POST /class-sessions/{id}/reservations`, `DELETE /class-sessions/{id}/reservations/{memberId}`, `POST /class-sessions/{id}/waitlist`. A rule the aggregate rejects is a problem-details 404, 409 or 422, never a 500: every `ReservationResult` and `WaitlistResult` value has its own case, and a value without one throws `UnreachableException` so a test catches it. `GET` is answered by `Application/GetClassSessionsQuery` and its handler (#254): only class sessions that start after now (strictly), by start, with `centerName` and `classTypeName` from `Application/IReferenceDataRepository` (the `centers` and `classTypes` collections; an unknown id gives an empty name). It keeps a cancelled class session in the list with `isCancelled: true` and `freeSeats: 0` (`ClassSession.FreeSeats`). Until #172 the member id comes in the request.
- **Configuration:** `MongoDb:Host`, `MongoDb:Port`, `MongoDb:Database`, `MongoDb:Username`, `MongoDb:Password` (Vault, #17) and the optional `MongoDb:AuthSource` (default: the database), as IdentityService (#253). `Seed:Enabled` loads the demo data at start-up (`Seed/ClassSessionSeeder`, #255): the centres "FitLife Center 1" to "6" and three class types (through `Application/IReferenceDataWriter`, the only writer of reference data), and two class sessions per centre and day for days 1 to 27, so each can be reserved. Every run tops the schedule up to 4 weeks from its own now and keeps what is stored, reservations included: seeded class sessions have ids made from their date, centre and slot. On in compose and in the `http` launch profile; start-up waits for it, so `/health` answers only once it has run.
- **Events:** the aggregate's `DomainEvents` are not published yet. That comes with the messaging issue.

## Talks to

The relations (Membership upstream, Notification downstream) are in the context map.

| Other context / system | How | Pattern | Why |
|---|---|---|---|
| Membership | REST: `GET members/by-account/{accountId}` with the member's own token passed on (`Clients/MembershipClient`, `IMembershipClient`). Address `Membership:BaseUrl`; time-out, retry and circuit breaker from `AddStandardResilienceHandler()` | Open Host Service (Membership upstream) | Who the member behind an account is, and whether the membership is active, before a reservation (#256, used by #260). The answer is found, not a member, or unavailable, never an exception |

## Rules for this folder

- Other services are reached over HTTP or RabbitMQ events only, never by a `ProjectReference` (FITLIFE001).
- `/health` stays mapped. `docker compose up --wait` and the Azure probes use it.
- The OpenAPI document and Scalar are mapped in Development only (course rule #7). `ApiDocumentationTests` checks it.
