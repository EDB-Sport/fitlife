# Event contracts

One JSON Schema per RabbitMQ event that crosses a context boundary (`docs/domain/CONTEXT-MAP.md` section 5, METHODOLOGY section 11.2 rule 7). The publishing context owns the schema.

- **File name:** the event name in kebab case, e.g. `ReservationMade` → `reservation-made.schema.json`.
- **Fields:** camelCase names from `GLOSSARY.md`; other aggregates and contexts by id only.
- **Strict:** `additionalProperties: false`, so a new field is a schema change that every consumer's tests see.
- **`examples`:** one sample payload. Consumer tests read it with `EventSchemas.Example("<file name>")`.
- **Headers:** the correlation ID travels in the `X-Correlation-ID` message header (#19), not in the body.

Tests: `tests/FitLife.Contracts.Tests` checks each schema against a sample in the test. Producer and consumer tests validate with `EventSchemas.Validate` from `tests/FitLife.Contracts.Testing`.

## On the wire (`libs/FitLife.Messaging`)

- **Exchange:** `fitlife.events` (durable topic). The routing key is the schema's file name, e.g. `reservation-made`.
- **Queues:** one durable quorum queue per consuming service and event, `<service>.<routing key>`, e.g. `notification.reservation-made`.
- **Failures:** a subscriber that throws gets the message again, up to 5 deliveries. After that, and straight away for a body that isn't the event, the message goes to `<queue>.dead-letter` (exchange `fitlife.events.dead-letter`). Nothing is dropped silently.
- **Dead letters:** look at them in the management UI (http://localhost:15672, *Queues and Streams*, `<queue>.dead-letter`, *Get messages*). To replay one, publish its body again to `fitlife.events` with the routing key, once the cause is fixed. The subscriber is idempotent, so a replay is safe.
