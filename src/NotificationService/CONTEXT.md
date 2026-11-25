# Notification (`src/NotificationService/`)

> From `docs/templates/CONTEXT.md`. Keep it under about 80 lines, and update it in the same PR that changes the model.

## Purpose

Member communication, a supporting subdomain: tells a member about what happened in Booking and Membership (context map). In the PoC the messaging channels are only logged (CONTEXT-MAP section 4).

## Language in this context

| Term | Meaning here | In code |
|---|---|---|
| Notifikation | One message to one member about one event. Stored, so the same event never gives two | `Notification` |
| Booking oprettet | Booking's event: a member got a seat. Read only, Booking owns it | `ReservationMade` |

Words we do **not** use here: *message*, *alert*, *push* for a notification (GLOSSARY Avoid column). "Message" means only the RabbitMQ message that carries an event.

## Aggregates

Plain types, no `*.Domain` layer yet (METHODOLOGY 11.2 rule 8): the only rule so far is idempotency.

### `Notification`

- **Referenced by Id only:** `MemberId` (Membership), the event's `ReservationId` (Booking)

| # | Invariant | Guarded by | Unit test |
|---|---|---|---|
| 1 | One event gives at most one notification | `Notification.Id` is the event's id; `INotificationRepository.TryAddAsync` refuses a second one (MongoDB `_id`) | `HandleAsync_TheSameReservationTwice_LogsOneNotification` |

**At most once, for now.** The notification is stored first and then sent (logged). If the process stops between the two, the redelivered event finds it stored and nothing is sent. Fine while sending is a log line; a real channel (SMS, e-mail, push) has to store after sending, or store a status.

## Commands, queries and events

| Kind | Name | Notes |
|---|---|---|
| Event (consumed) | `ReservationMade` | Queue `notification.reservation-made`. Logs one line with the member id and the correlation ID |

## Talks to

| Other context / system | How | Pattern | Why |
|---|---|---|---|
| Booking | RabbitMQ events (`libs/FitLife.Messaging`) | Customer–Supplier | A reservation must not wait for a message to be sent |
| Messaging channels (SMS, e-mail, push) | Log only, in the PoC | ACL | External formats stay outside our model |

## Rules for this folder

- Other services are reached over HTTP or RabbitMQ events only, never by a `ProjectReference` (FITLIFE001). Events come in through `libs/FitLife.Messaging`, never RabbitMQ.Client directly.
- MongoDB only in `NotificationService.Repositories` (TEAM001).
- `/health` stays mapped. `docker compose up --wait` and the Azure probes use it.
- The OpenAPI document and Scalar are mapped in Development only (course rule #7). `ApiDocumentationTests` checks it.
