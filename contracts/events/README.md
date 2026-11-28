# Event contracts

One JSON Schema per RabbitMQ event that crosses a context boundary (`docs/domain/CONTEXT-MAP.md` section 5, METHODOLOGY section 11.2 rule 7). The publishing context owns the schema.

- **File name:** the event name in kebab case, e.g. `ReservationMade` → `reservation-made.schema.json`.
- **Fields:** camelCase names from `GLOSSARY.md`; other aggregates and contexts by id only.
- **Strict:** `additionalProperties: false`, so a new field is a schema change that every consumer's tests see.
- **`examples`:** one sample payload, from the record's `Example`. Consumer tests read it with `EventSchemas.Example("<file name>")`.
- **Headers:** the correlation ID travels in the `X-Correlation-ID` message header (#19), not in the body.

Tests: `tests/FitLife.Contracts.Tests` checks each schema against a sample in the test. Producer and consumer tests validate with `EventSchemas.Validate` from `tests/FitLife.Contracts.Testing`.

An event is done when a flow test in `FitLife.Flows.IntegrationTests` passes: the publisher's service and the consumer's, in one test, over the real RabbitMQ (#192). The consumer may merge first (#493).

## Handled once on

RabbitMQ can deliver an event more than once, so each consumer handles it only once on these fields (#231):

| Event | Published by | Handled once on |
|---|---|---|
| `reservation-made` | Booking | `reservationId` |
| `reservation-cancelled` | Booking | `reservationId` (the id `reservation-made` carried; a reservation is cancelled once) |
| `class-session-cancelled` | Booking | `classSessionId`, and `memberId` per notice |
| `class-session-changed` | Booking | `classSessionId` + `startTime` |
| `membership-paused` | Membership | `memberId` + `from` |
| `membership-cancelled` | Membership | `memberId` |
| `payment-failed` | Membership | `memberId` + `failedAt` |

Dates without a time (`from`, `to`, `lastTrainingDay`) are `format: date` (`DateOnly`); moments are `format: date-time` with an offset (`DateTimeOffset`). A list of ids (`memberIds`) may be empty; each id in it has at least one character.

## Generated, don't edit by hand (#491)

Each schema is generated from its integration event record in `libs/FitLife.Messaging/Events/`, the record the publisher sends and the consumer reads. Change the record, then regenerate:

```bash
dotnet build tools/EventSchemaGenerator    # or a plain `dotnet build`: it writes contracts/events/ too
```

Commit the changed `*.schema.json` with the record. The `contract` job (`tools/ci/check-contracts.sh`) builds, and it fails when `contracts/events/` then differs from the commit.

- **Generator:** `EventSchema` in `libs/FitLife.Messaging`, with `System.Text.Json.Schema.JsonSchemaExporter` and the wire format's serializer options (`EventJson`). Fields come from the constructor, all required. `additionalProperties: false` is set in the exporter's `TransformSchemaNode` callback.
- **From the record:** the `[Description]` on the record and on each property, `[MinLength]` on a property, and the static `Example` (`IIntegrationEvent.Example`), which becomes `examples`.
- **A new event:** a record that implements `IIntegrationEvent`, plus one line in `EventSchema.GenerateAll`. A test fails until that line is there.

## On the wire (`libs/FitLife.Messaging`)

- **Exchange:** `fitlife.events` (durable topic). The routing key is the schema's file name, e.g. `reservation-made`.
- **Queues:** one durable quorum queue per consuming service and event, `<service>.<routing key>`, e.g. `notification.reservation-made`.
- **Failures:** a subscriber that throws gets the message again: 6 deliveries in all, the first plus 5 redeliveries (`x-delivery-limit: 5`), straight after each other. After that, and straight away for a body that isn't the event, the message goes to `<queue>.dead-letter` (exchange `fitlife.events.dead-letter`). Nothing is dropped silently.
- **Dead-lettering is at-most-once** (RabbitMQ's default for quorum queues): RabbitMQ would drop a dead letter whose target queue is missing. The consumer declares `<queue>.dead-letter` before it consumes, and it has no length limit. `x-dead-letter-strategy: at-least-once` (with `x-overflow: reject-publish`) is the setting if that is ever not enough.
- **Dead letters:** look at them in the management UI (http://localhost:15672, *Queues and Streams*, `<queue>.dead-letter`, *Get messages*). To replay one, publish its body again to `fitlife.events` with the routing key, once the cause is fixed. The subscriber is idempotent, so a replay is safe.
