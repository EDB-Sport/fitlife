# Notification (`src/NotificationService/`)

> From `docs/templates/CONTEXT.md`. Keep it under about 80 lines, and update it in the same PR that changes the model.

## Purpose

Member communication, a supporting subdomain: tells a member about what happened in Booking and Membership (context map). In the PoC the messaging channels are only logged (CONTEXT-MAP section 4).

## Language in this context

| Term | Meaning here | In code |
|---|---|---|
| Notifikation | One message to one member about one event. Stored, so the same event never gives two | `Notification` |
| Booking oprettet | Booking's event: a member got a seat. Read only, Booking owns it | `ReservationMade` |
| Booking afmeldt | Booking's event: a member's reservation is cancelled. A Sen afmelding gets the same text | `ReservationCancelled` |
| Hold aflyst | Booking's event: a class session will not take place; everyone reserved or waitlisted is told | `ClassSessionCancelled` |
| Hold ændret | Booking's event: a class session got a new start; everyone reserved is told and keeps the reservation | `ClassSessionChanged` |

Words we do **not** use here: *message*, *alert*, *push* for a notification (GLOSSARY Avoid column). "Message" means only the RabbitMQ message that carries an event.

## Aggregates

Plain types, no `*.Domain` layer yet (METHODOLOGY 11.2 rule 8): the only rule so far is idempotency.

### `Notification`

- **Referenced by Id only:** `MemberId` (Membership), the event's `ReservationId` or `ClassSessionId` (Booking)

| # | Invariant | Guarded by | Unit test |
|---|---|---|---|
| 1 | One event gives at most one notification per member | `Notification.Id` is the key the event's schema names (contracts/events/README.md), with the member id for an event that lists members; `INotificationRepository.TryAddAsync` refuses a second one (MongoDB `_id`) | `HandleAsync_TheSameReservationTwice_LogsOneNotification`, `HandleAsync_ClassSessionCancelledTwice_StoresNothingNew` |

**At most once, for now.** The notification is stored first and then sent (logged). If the process stops between the two, the redelivered event finds it stored and nothing is sent. Fine while sending is a log line; a real channel (SMS, e-mail, push) has to store after sending, or store a status.

## Commands, queries and events

| Kind | Name | Notes |
|---|---|---|
| Event (consumed) | `ReservationMade` | Queue `notification.reservation-made`. Logs one line with the member id and the correlation ID, and stores the notification with its kind (`ReservationMade`) and time |
| Event (consumed) | `ReservationCancelled` | Queue `notification.reservation-cancelled`. One notification to the member, id `reservation-cancelled:<reservationId>` |
| Event (consumed) | `ClassSessionCancelled` | Queue `notification.class-session-cancelled`. One notification per listed member, id `class-session-cancelled:<classSessionId>:<memberId>`; none when the list is empty |
| Event (consumed) | `ClassSessionChanged` | Queue `notification.class-session-changed`. One notification per listed (reserved) member, id `class-session-changed:<classSessionId>:<startTime as UTC>:<memberId>`; none when the list is empty |
| Query | `GET /notifications?memberId={id}` | The delivery log: a member's notifications, newest first. Træner and Admin only (docs/domain/ACCESS.md). Through the gateway at `/api/notification/notifications` |

## Talks to

| Other context / system | How | Pattern | Why |
|---|---|---|---|
| Booking | RabbitMQ events (`libs/FitLife.Messaging`) | Customer–Supplier | A reservation must not wait for a message to be sent |
| Identity | The token, checked with `libs/FitLife.Authentication` | Conformist | Every service checks the same token (CONTEXT-MAP section 4) |
| Messaging channels (SMS, e-mail, push) | Log only, in the PoC | ACL | External formats stay outside our model |

## Rules for this folder

- Other services are reached over HTTP or RabbitMQ events only, never by a `ProjectReference` (FITLIFE001). Events come in through `libs/FitLife.Messaging`, never RabbitMQ.Client directly.
- MongoDB only in `NotificationService.Repositories` (TEAM001).
- `/health` stays mapped and open (`AllowAnonymous()`). `docker compose up --wait` and the Azure probes use it.
- Every other endpoint needs a token (`AddFitLifeAuthorization()`); the delivery log needs Træner or Admin.
- The OpenAPI document and Scalar are mapped in Development only (course rule #7). `ApiDocumentationTests` checks it.
