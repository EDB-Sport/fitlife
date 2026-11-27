# <Context name> (`src/<Service>/`)

> **Template.** Copy it to `src/<Service>/CONTEXT.md`, one for each bounded context, and fill it in when the context's tactical model is made (METHODOLOGY section 11).
> Keep it short, under about 80 lines. People and agents read it before every task in this folder.
> **Point your agent at it.** In Claude Code, for example, put `@CONTEXT.md` in a `CLAUDE.md` in the same folder, which loads only when the agent works there. Other tools have an equivalent. Whether those shim files are committed is a group decision (`METHODOLOGY.md` section 9).
> Update this file in the same PR that changes the model.

## Purpose

One or two lines: which part of the business this context serves, and which subdomain it implements (see `docs/domain/CONTEXT-MAP.md`).

## Language in this context

The words that mean something specific **here**. The full rows, with the names to avoid, are in `GLOSSARY.md`.

| Term | Meaning here | In code |
|---|---|---|
| | | |

Words from other contexts that we do **not** use here, and what we call the thing instead:

- *e.g. "session": here it always means `ClassSession`, never a login session*

## Aggregates

### `<RootName>` (aggregate root)

- **Children** (entities that don't exist without the root):
- **Value objects:**
- **Referenced by Id only** (other aggregates or other contexts): e.g. `MemberId`, owned by Membership

**Invariants.** These rules hold after every completed change. Write them the way the business says them.

| # | Invariant | Guarded by method | Unit test |
|---|---|---|---|
| 1 | *e.g. A class session never has more reservations than seats* | `ClassSession.Reserve(...)` | `Reserve_WhenFull_IsRejected` |
| 2 | | | |

**Methods on the root** (business actions; nothing else changes the state):

- `Reserve(memberId)`, `Cancel(reservationId)`, ...

**What happens on concurrent changes:** *e.g. optimistic concurrency on a version field; the second writer gets 409*

## Commands, queries and events

| Kind | Name | Notes |
|---|---|---|
| Command | `<Verb><Noun>Command` | |
| Query | `Get<Noun>Query` | |
| Event published | `<Noun><PastTense>` | Schema: `contracts/events/...` |
| Event consumed | | From which context, and what we do with it |

## Talks to

| Other context / system | How | Pattern | Why |
|---|---|---|---|
| | REST via typed client / event | Customer–Supplier / ACL / ... | |

## Rules for this folder

1. Change state only through methods on the aggregate root. Types in `*.Domain` have no public setters.
2. Business rules live in the domain model, not in controllers or application services.
3. Reference other aggregates and other contexts by Id only. Never hold their objects.
4. One repository for each aggregate root (`I<Root>Repository`). No repository for a child.
5. Don't reference code under another `src/<Service>/`. Use its API or its events. The build fails anyway.
6. Translate data from external systems at the boundary (ACL). Their names never reach the domain model.
7. Use only names from `GLOSSARY.md`. Need a new term? Add the row in the same PR. Never invent a synonym.
8. Every invariant has a unit test, written first. It must fail if the rule is removed.
9. Unsure about a business rule? Stop and ask the group. Don't guess.
