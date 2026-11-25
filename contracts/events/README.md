# Event contracts

One JSON Schema per RabbitMQ event that crosses a context boundary (`docs/domain/CONTEXT-MAP.md` section 5, METHODOLOGY section 11.2 rule 7). The publishing context owns the schema.

- **File name:** the event name in kebab case, e.g. `ReservationMade` → `reservation-made.schema.json`.
- **Fields:** camelCase names from `GLOSSARY.md`; other aggregates and contexts by id only.
- **Strict:** `additionalProperties: false`, so a new field is a schema change that every consumer's tests see.
- **`examples`:** one sample payload. Consumer tests read it with `EventSchemas.Example("<file name>")`.
- **Headers:** the correlation ID travels in the `X-Correlation-ID` message header (#19), not in the body.

Tests: `tests/FitLife.Contracts.Tests` checks each schema against a sample in the test. Producer and consumer tests validate with `EventSchemas.Validate` from `tests/FitLife.Contracts.Testing`.
