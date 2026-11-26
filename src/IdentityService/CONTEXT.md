# Identity (`src/IdentityService/`)

> From `docs/templates/CONTEXT.md`. Keep it under about 80 lines, and update it in the same PR that changes the model.
> **Status: accounts, tokens, demo accounts the list of accounts and the role Træner (#233, #234, #235, #237, #364, #365).** `POST /accounts` creates an account with the role Medlem. `POST /tokens` logs in and gives a JWT with the account's id and roles. `GET /accounts` lists every account for an Admin. An Admin gives or takes the role Træner with `PUT` and `DELETE /accounts/{id}/roles/traener`. With `Seed:DemoAccounts` true, one demo account per role is seeded after start-up. The context map (`docs/domain/CONTEXT-MAP.md`) is decided in ADR 007.

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
| A demo account has exactly the one role it is created with | `AccountTests.Create_WithARole_HasOnlyThatRole` |
| An account has an e-mail address and a password hash, never the password | `AccountTests.Create_WithoutAPasswordHash_Throws`, `Create_WithoutAnEmail_Throws` |
| Giving a role the account has changes nothing (#365) | `AccountRoleTests.GiveRole_ARoleItHas_ChangesNothing` |
| An account always keeps at least one role: taking its last role is rejected (#365) | `AccountRoleTests.TakeRole_TheLastRole_IsRejected` |
| A role is exactly Medlem, Træner or Admin, with the glossary's names | `RoleTests.All_IsMedlemTraenerAndAdmin_WithTheGlossaryNames`, `FromName_WithAnotherName_Throws` |
| The names of `Role.All` are the role claim values of `FitLifeRoles`, so Identity and the library can't drift | `RoleTests.Role_All_MatchesFitLifeRoles` |
| E-mail addresses that differ only in letter case are the same e-mail address; a unique index guards it | `MongoAccountRepositoryTests.AddAsync_WithTheSameEmailInAnotherLetterCase_IsRejected`, `FindByEmailAsync_WithAnotherLetterCase_ReturnsTheAccount` |

## Commands, queries and events

| Kind | Name | Endpoint | What it does |
|---|---|---|---|
| Command | `CreateAccountCommand` (`CreateAccountHandler`) | `POST /accounts` | Hashes the password with `PasswordHasher<Account>` and stores a new account with the role Medlem. `EmailAlreadyTaken` (409) when another account has the e-mail address in any letter case. The password and the hash are never in a response or a log line |
| Command | `LogInCommand` (`LogInHandler`) | `POST /tokens` | Checks the password against the stored hash and, with `TokenWriter`, signs a JWT with the `FitLifeJwt` values: `sub` = the account's id, one `role` claim per role, valid for 60 minutes from `TimeProvider`'s now. An unknown e-mail address and a wrong password give the same 401. The key is read from `Jwt:SigningKey` when a token is written, so the service starts without one |
| Query | `ListAccountsQuery` (`ListAccountsHandler`) | `GET /accounts` | Every account as `{ id, email, roles }`, ordered by e-mail address. Needs a token with the role Admin: 401 without a token, 403 without Admin. Never the password or the hash. Identity checks tokens closed by default (`AddFitLifeJwtAuthentication()`, `AddFitLifeAuthorization()`); `POST /accounts`, `POST /tokens`, `/health` and, in Development, the OpenAPI document and Scalar are marked open |
| Command | `GiveRoleCommand` (`ChangeRoleHandler`) | `PUT /accounts/{id}/roles/traener` | An Admin gives the account the role Træner (#365): `Account.GiveRole`, then `IAccountRepository.SaveRolesAsync` only when the roles changed. 204, also when it had the role; 404 for an unknown id; 401 and 403 as `GET /accounts`. One Information line per change with both account ids and the change, never an e-mail address. The token carries the roles from log-in, so the change applies from the next log-in (ADR 009) |
| Command | `TakeRoleCommand` (`ChangeRoleHandler`) | `DELETE /accounts/{id}/roles/traener` | An Admin takes the role Træner (#365): `Account.TakeRole`. 204, also when it never had the role; 409 when Træner is its only role, since an account always keeps one (`RoleChange.LastRoleKept`) |
| Seed | `DemoAccountSeeder` (`DemoAccountSeedService`) | – | Only when `Seed:DemoAccounts` is `true`: after start-up, adds `medlem@`, `traener@` and `admin@fitlife.example`, each with its one role and a password from `Seed:MedlemPassword`, `Seed:TraenerPassword` or `Seed:AdminPassword`. An account that exists is left as it is. `/health` is unhealthy until the seed has finished; a failed seed is logged as an error and the service keeps running |

Identity publishes no events: Membership reads the account's id from the token.

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
