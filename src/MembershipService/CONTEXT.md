# Membership (`src/MembershipService/`)

> From `docs/templates/CONTEXT.md`. Keep it under about 80 lines, and update it in the same PR that changes the model.

## Purpose

Membership, a supporting subdomain: who is a member of FitLife, and whether their membership gives access and the right to book (context map, section 3). It replaces the 2012 membership system's member register.

## Language in this context

| Term | Meaning here | In code |
|---|---|---|
| Medlem | A person who has joined FitLife. Keeps the id of the account that joined | `Member` |
| Medlemskab | The member's agreement: type, status and start date | `Membership` |
| Medlemstype | The kind of membership. The case has exactly one | `MembershipType` (`Standard`) |
| Medlemskabets tilstand | Where the membership is in its life. Aktiv and Betaling mangler so far | `MembershipStatus` (`Active`, `PaymentMissing`) |
| Betalingsudbyder | The external payment provider that charges the fee | `IPaymentProvider`, implemented by the ACL `PaymentProviderClient` |
| Indmeldelse | A person with an account joins with their token | `JoinMembershipCommand` |

Words from other contexts that we do **not** use here: *user* or *login* (that is Identity's `Account`; we keep only its id), *subscription* (say `Membership`).

## Aggregates

| Root | Holds | Invariants |
|---|---|---|
| `Member` | The member id and the account id | An account id belongs to at most one member (the handler checks, and a unique index on `members.AccountId` catches a race) |
| `Membership` | Member id (by id only), type, status, start date | A new membership starts the day the member joins with `PaymentMissing`, which gives no access and no right to book, and becomes `Active` when the first fee is paid (`MarkPaid`). Stored before the charge and made active only after it, so an interrupted join never leaves an active, unpaid membership |

## Commands, queries and events

| Kind | Name | Notes |
|---|---|---|
| Command | `JoinMembershipCommand` | `POST /members`. The account id comes from the token (`sub`), never from the body. 409 when the account is already a member. Then the first fee (`Membership:MonthlyFeeDkk`) is charged; paid → `Active`, otherwise it stays `PaymentMissing` |
| Query | by account id | `GET /members/by-account/{accountId}`: the `MemberId` and whether the membership is active, 404 for an unknown account. Only for the account in the token: another account answers 404 too, so a member can't look up other members. Booking asks this before a reservation (#173) |
| Query | my membership | `GET /members/me`: the membership of the account in the token (`sub`, never the path): `memberId`, `membershipType`, `membershipStatus`, `isActive`, `startDate`. 404 when the account hasn't joined. The member portal's "Mit medlemskab" reads it (#210) |

No events yet. `MembershipPaused` and `MembershipCancelled` come with pause (UC6) and cancellation (UC8).

## Talks to

| Other context / system | How | Pattern | Why |
|---|---|---|---|
| Identity | The JWT, checked with `libs/FitLife.Authentication` | Open Host Service (upstream) | Every service checks the same token the same way |
| Booking; Access (NEXT) | REST, the lookup by account id | Open Host Service (we are upstream) | Several contexts need the same answer, synchronously |
| Payment provider | REST, `POST /v1/charges` at `PaymentProvider:BaseUrl`; a Postman Mock Server in the PoC (`tests/postman/mocks/payment-provider.*`) | ACL: `PaymentProvider/PaymentProviderClient` is the only class that knows its field names | External system (P5). Declined, a provider error and no answer all mean the fee is not paid |

## Rules for this folder

- Both endpoints need a valid token. The password never passes through Membership.
- MongoDB types only in `MembershipService.Repositories` (TEAM001). The domain is stored through document classes there, so `*.Domain` has no MongoDB attributes and no public setters.
- Database `membership`, user `membership-svc`. The password is `MongoDb:Password` from Vault.
- `/health` stays mapped. The OpenAPI document and Scalar are mapped in Development only (course rule #7).
