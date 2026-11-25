# Identity (`src/IdentityService/`)

> From `docs/templates/CONTEXT.md`. Keep it under about 80 lines, and update it in the same PR that changes the model.
> **Status: the `Account` model and its repository (#233).** Nothing is reachable over HTTP yet: the service answers `/health` only. `POST /accounts` is #234, `POST /tokens` is #235. The context map (`docs/domain/CONTEXT-MAP.md`) is decided in ADR 007.

## Purpose

Log-in, roles and tokens for FitLife Digital: the generic subdomain "Identity and access" (CONTEXT-MAP sections 2 and 3), built simply ourselves with JWT. Small in the PoC: test users and roles.

## Language in this context

The rows are in `GLOSSARY.md`; this is the part Identity uses.

| Term | Meaning here | In code |
|---|---|---|
| Konto | The log-in identity of a person: e-mail address (the log-in name), password hash and roles | `Account` |
| Rolle | What an account may do: Medlem, Træner or Admin, carried as a claim in the token | `Role` |

Words from other contexts that we do **not** use here:

- *"User", "Login"*: here it is always a `Konto` (`Account`). The `Medlem` belongs to Membership, which keeps the account's id from the token (CONTEXT-MAP section 3, "Sign-up takes two steps").

## Aggregates

**`Account`** (aggregate root, `Domain/Account.cs`) with its `Role`s (value object, `Domain/Role.cs`). Stored by `IAccountRepository` (`Application/`), implemented by `MongoAccountRepository` (`Repositories/`, the only code that uses `MongoDB.Driver`).

| Rule | Test |
|---|---|
| A new account has the role Medlem | `AccountTests.Create_WithAnEmailAndAPasswordHash_HasRoleMedlem` |
| An account has an e-mail address and a password hash, never the password | `AccountTests.Create_WithoutAPasswordHash_Throws`, `Create_WithoutAnEmail_Throws` |
| A role is exactly Medlem, Træner or Admin, with the glossary's names | `RoleTests.All_IsMedlemTraenerAndAdmin_WithTheGlossaryNames`, `FromName_WithAnotherName_Throws` |
| E-mail addresses that differ only in letter case are the same e-mail address; a unique index guards it | `MongoAccountRepositoryTests.AddAsync_WithTheSameEmailInAnotherLetterCase_IsRejected`, `FindByEmailAsync_WithAnotherLetterCase_ReturnsTheAccount` |

## Commands, queries and events

None yet. Creating an account (#234) and issuing a token (#235) come next. Identity publishes no events: Membership reads the account's id from the token.

## Talks to

| Other context / system | How | Pattern | Why |
|---|---|---|---|
| Every service | The JWT it issues, checked by `libs/FitLife.Authentication` (#183) | Open Host Service (JWT) | Each service checks the same token in the same way (CONTEXT-MAP section 4) |

## Rules for this folder

1. Change state only through methods on the aggregate root. Types in `*.Domain` have no public setters.
2. Business rules live in the domain model, not in controllers or application services.
3. Reference other aggregates and other contexts by Id only. Never hold their objects.
4. One repository for each aggregate root (`I<Root>Repository`). No repository for a child.
5. Don't reference code under another `src/<Service>/`. Use its API or its events. The build fails anyway.
6. Use only names from `GLOSSARY.md`. Need a new term? Add the row in the same PR. Never invent a synonym.
7. Every invariant has a unit test, written first. It must fail if the rule is removed.
8. A password is stored only as a hash, and never logged (TEAM003).
