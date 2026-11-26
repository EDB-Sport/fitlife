# ADR 009: Log-in and roles

- **Status:** accepted
- **Date:** 2026-11-26
- **Deciders:** Jesper (proposal), and the reviewers who sign off the PR (issue #363)

## Context

The report and the oral exam ask why log-in works as it does, and every feature asks who may use it. This ADR records the decision. [`docs/domain/ACCESS.md`](../domain/ACCESS.md) holds the table of which role may call which endpoint or open which page.

Forces:
- Identity is a Generic subdomain: "Build simply ourselves" (CONTEXT-MAP section 2). Principle P5 in part 1 says we build what sets FitLife apart and buy or integrate the rest, so a generic subdomain is usually bought. We build this one because the course teaches JWT authentication [M10.03], and the PoC needs only test users and roles (CONTEXT-MAP section 2, ADR 007).
- TO-BE-kontekst §1 decides the form: JWT as the token format and the roles Medlem, Træner and Admin as claims. Principle PS1: least privilege, and roles are checked in the backend. §5 asks for role-based access in the backend.
- Every service must check the same token in the same way (CONTEXT-MAP section 4: Identity is an Open Host Service).
- The class schedule can be seen without logging in (TO-BE-kontekst §8).

## Decision

Our own `IdentityService` issues the token, and every service checks it itself.

- **The token.** `POST /tokens` gives a JWT signed with HMAC-SHA256 and one key shared by all services, `Jwt:SigningKey`, which Vault delivers. The account id is in `sub` and each role is its own `role` claim (`FitLifeJwt`, `libs/FitLife.Authentication`).
- **The check.** Every service calls `AddFitLifeJwtAuthentication()` and `AddFitLifeAuthorization()`. The policies are `Medlem`, `TraenerOrAdmin` and `Admin` (`FitLifePolicies`), and a fallback policy needs a valid token, so an endpoint is closed until it is opened on purpose with `AllowAnonymous()`. A request without a token gets 401, and a token without the role gets 403. The roles are checked in the backend, never in a UI (PS1).
- **The lifetime.** The token lasts 60 minutes. There is no refresh token (#235).
- **The member portal.** The portal keeps the token in its authentication cookie, which is HttpOnly, and adds it to its calls to the services (ADR 003).

Where the code is on `develop` today: Membership has the check on every endpoint (#368). Identity, Booking and Notification take it up in their own issues (#364, #369, #221), and the portal's log-in is #206. Until then their endpoints are as `docs/domain/ACCESS.md` shows them.

## Alternatives considered

- **MitID** instead of our own accounts. TO-BE-kontekst §1 says "MitID i produktion, testbrugere i PoC", and §10 leaves MitID out of the PoC. We chose test accounts with roles over MitID because that is the scope the TO-BE context document gives the PoC.
- **A bought identity provider** instead of our own `IdentityService`. Rejected in ADR 007: we chose our own JWT service over a bought provider because the course teaches JWT authentication [M10.03] and the PoC needs only test users and roles.

## Consequences

- A role change applies from the account's next log-in, because the token carries the roles from the log-in. A token already issued keeps its old roles for up to 60 minutes (#365).
- Anyone with the key can sign a token for any account and role, so the services read the key only from Vault, which is seeded from the secret `JWT_SIGNING_KEY`, and the key is never committed.
- Log-out removes the portal's cookie, and the token itself is valid until it expires.
