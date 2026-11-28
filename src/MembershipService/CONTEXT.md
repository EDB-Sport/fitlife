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
| Medlemskabets tilstand | Where the membership is in its life, on a given day: Aktiv, Betaling mangler, Pause, Opsagt, Afsluttet | `MembershipStatus` (`Active`, `PaymentMissing`, `Paused`, `Cancelled`, `Ended`) |
| Betalingsudbyder | The external payment provider that charges the fee | `IPaymentProvider`, implemented by the ACL `PaymentProviderClient` |
| Indmeldelse | A person with an account joins with their token | `JoinMembershipCommand` |
| Pause | A period, both days included, in which the membership gives no access and no right to book (UC6) | `Membership.PausedFrom`, `PausedTo` |

Words from other contexts that we do **not** use here: *user* or *login* (that is Identity's `Account`; we keep only its id), *subscription* (say `Membership`).

## Aggregates

| Root | Holds | Invariants |
|---|---|---|
| `Member` | The member id and the account id | An account id belongs to at most one member (the handler checks, and a unique index on `members.AccountId` catches a race) |
| `Membership` | Member id (by id only), type, stored status, start date, one pause period, the last training day | A new membership starts the day the member joins with `PaymentMissing`, which gives no access and no right to book, and becomes `Active` when the first fee is paid (`MarkPaid`). Stored before the charge and made active only after it, so an interrupted join never leaves an active, unpaid membership. A paid missing fee (`RegisterPayment`) makes `PaymentMissing` `Active` again; any other status owes nothing and doesn't change (`RegisterPayment_WhenPaymentIsMissing_MakesTheMembershipActive`, `RegisterPayment_WhenNothingIsOwed_IsRejected`) |
| | | The status on a day (`StatusOn`, `IsActiveOn`) is computed from the dates, no background job: `Paused` from `PausedFrom` to `PausedTo` when the stored status is `Active` (`Pause_WhenActive_IsPausedOnlyInsideThePeriod`) |
| | | `Pause(from, to, today)` is rejected, changing nothing, when the membership isn't `Active` today (`Pause_WhenNotActive_IsRejected`, `Pause_WhilePaused_IsRejected`), when `from` is before today (`Pause_StartingBeforeToday_IsRejected`) or when `to` isn't after `from` (`Pause_EndingOnOrBeforeItsStart_IsRejected`). One pause period: a new one replaces one still to come |
| | | `EndPause(today)` removes a pause period that isn't over, so the membership is `Active` from today (`EndPause_WhilePaused_MakesTheMembershipActiveFromToday`, `EndPause_BeforeThePauseStarts_RemovesIt`); otherwise `NoPauseToEnd` (`EndPause_WithNoPause_IsRejected`) |
| | | `Cancel(today)` (UC8): one month's notice from the first day of the coming month, so `LastTrainingDay` is the last day of the month after today's (`Cancel_OnADay_SetsTheLastTrainingDayToTheEndOfTheNextMonth`). Aktiv, Pause and Betaling mangler can be cancelled (`Cancel_WhilePaused_IsCancelled`); the stored status becomes `Cancelled`, which gives no access (`IsActive_AfterCancelling_IsFalse`). `Cancelled` up to and including the last training day, inside a pause period too, `Ended` from the day after (`StatusOn_OnTheLastTrainingDay_IsCancelled`, `StatusOn_TheDayAfterTheLastTrainingDay_IsEnded`). Rejected with `MembershipAlreadyCancelled`, the last training day unchanged, when the status today is `Cancelled` or `Ended` (`Cancel_WhenAlreadyCancelled_IsRejected`, `Cancel_AfterTheLastTrainingDay_IsRejected`) |

## Commands, queries and events

| Kind | Name | Notes |
|---|---|---|
| Command | `JoinMembershipCommand` | `POST /members`. The account id comes from the token (`sub`), never from the body. 409 `AccountIsAlreadyAMember` when the account is already a member (#542). Then the first fee (`Membership:MonthlyFeeDkk`) is charged; paid → `Active`, otherwise it stays `PaymentMissing` |
| Command | `PayMembershipCommand` | `POST /members/me/payment`, no body: the account in the token pays the missing fee (UC18). 409 `code` `NothingIsOwed` unless the status is `PaymentMissing`, and then nothing is charged. Accepted → `Active`, 200 with the body of `GET /members/me`; not accepted → nothing saved, 409 `code` `ChargeDeclined`. 404 when the account hasn't joined |
| Command | `PauseMembershipCommand` | `PUT /members/me/pause` `{ "from": "2026-12-01", "to": "2026-12-31" }`: 200 with the body of `GET /members/me` plus `pausedFrom` and `pausedTo`, or 409 problem details whose `code` is the `PauseResult` name. A missing or malformed date is 400 |
| Command | `EndPauseCommand` | `DELETE /members/me/pause`: 200 with the same body, or 409 with `"code": "NoPauseToEnd"`. Both commands: 404 when the account hasn't joined; "today" is the UTC date from `TimeProvider` |
| Command | `CancelMembershipCommand` | `POST /members/me/cancellation`, no body (UC8): 200 `{ "lastTrainingDay": "2026-12-31" }`, or 409 with `"code": "MembershipAlreadyCancelled"`. 404 when the account hasn't joined; "today" is the UTC date from `TimeProvider` |
| Query | by account id | `GET /members/by-account/{accountId}`: the `MemberId`, whether the membership is active today (`Paused` inside a pause period), and `pausedFrom` and `pausedTo` while a pause period is running or still to come (`HasPauseNotOverOn`), else both null (#536), 404 for an unknown account. Only for the account in the token: another account answers 404 too, so a member can't look up other members. Booking asks this before a reservation (#173) |
| Query | my membership | `GET /members/me`: the membership of the account in the token (`sub`, never the path): `memberId`, `membershipType`, `membershipStatus`, `isActive`, `startDate`, `lastTrainingDay` (null unless cancelled). 404 when the account hasn't joined. The member portal's "Mit medlemskab" reads it (#210) |
| Event published | `MembershipPaused` (`contracts/events/membership-paused.schema.json`) | After a pause is saved: `memberId`, `from`, `to` (#251). Not for a rejected pause or a pause ended early |
| Event published | `PaymentFailed` (`contracts/events/payment-failed.schema.json`) | After a join is saved with `PaymentMissing` because the first charge didn't go through: `memberId`, `failedAt` (#251). Not for a declined `POST /members/me/payment`, which saves nothing |

Both are published after the save, through `Application/MembershipEventPublisher` and `libs/FitLife.Messaging`, without the request's token. A failed publish is logged and the change is kept, so the member's answer is the same; there is no outbox, so that event is lost (#192's safe default). `MembershipCancelled` is not published yet: `POST /members/me/cancellation` saves and answers, and the event follows in #649.

## Talks to

| Other context / system | How | Pattern | Why |
|---|---|---|---|
| Identity | The JWT, checked with `libs/FitLife.Authentication` | Open Host Service (upstream) | Every service checks the same token the same way |
| Booking; Access (NEXT) | REST, the lookup by account id | Open Host Service (we are upstream) | Several contexts need the same answer, synchronously |
| Booking, Notification | RabbitMQ events (`MembershipPaused`, `PaymentFailed`) on `fitlife.events` | Customer-Supplier (we are upstream) | Booking frees a paused member's seats (#243); Notification tells the member (#224) |
| Payment provider | REST, `POST /v1/charges` at `PaymentProvider:BaseUrl`; a Postman Mock Server in the PoC (`tests/postman/mocks/payment-provider.*`) | ACL: `PaymentProvider/PaymentProviderClient` is the only class that knows its field names | External system (P5). Declined, a provider error and no answer all mean the fee is not paid. Once a charge starts, a cancelled request stops neither the charge nor the save of its result, so a paid fee is never left as `PaymentMissing`; the client's 10 s time-out still applies (#544) |

## Rules for this folder

- Every endpoint needs a valid token. The password never passes through Membership.
- MongoDB types only in `MembershipService.Repositories` (TEAM001). The domain is stored through document classes there, so `*.Domain` has no MongoDB attributes and no public setters.
- Database `membership`, user `membership-svc`. The password is `MongoDb:Password` from Vault.
- `/health` stays mapped. The OpenAPI document and Scalar are mapped in Development only (course rule #7).
