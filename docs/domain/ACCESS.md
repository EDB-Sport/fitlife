# Access: who may call which endpoint or open which page

The one table of which role may call which endpoint or open which page. [ADR 009](../adr/009-log-in-and-roles.md) records how log-in and roles work. A PR that adds or changes an endpoint or a page updates its row (`docs/PR_RULES.md`).

This table is a reference, not a second spec. When an issue and the table disagree, the issue wins and the table is fixed.

**How to read it**
- **Open** `yes`: no token is needed, so every role may call it and the three role cells say `yes`. `–` in Open: a valid token is needed.
- A role cell is `yes` (the role may), `–` (the role may not) or `not decided`. `not decided` means the issue in the last column names no role. It links the issue, because the builder doesn't decide it.
- A row marked **(planned)** is not on `develop` yet. Its cells are what its issue says.
- A row without "(planned)" shows the code on `develop`. Where the Issue column also names an open issue that changes it, the cells show the code today and the issue says what changes.
- Paths are the service's own. The gateway adds `/api/<service>/` in front (for example `/api/booking/class-sessions`). Roles are the glossary's values: Medlem, Træner, Admin (`FitLifeRoles`).

| Endpoint or page | Service | Open | Medlem | Træner | Admin | Issue |
|---|---|---|---|---|---|---|
| `POST /accounts` | IdentityService | yes | yes | yes | yes | [#234](https://github.com/EDB-Sport/fitlife/issues/234) |
| `POST /tokens` | IdentityService | yes | yes | yes | yes | [#235](https://github.com/EDB-Sport/fitlife/issues/235) |
| `GET /accounts` | IdentityService | – | – | – | yes | [#364](https://github.com/EDB-Sport/fitlife/issues/364) |
| `PUT /accounts/{id}/roles/traener` | IdentityService | – | – | – | yes | [#365](https://github.com/EDB-Sport/fitlife/issues/365) |
| `DELETE /accounts/{id}/roles/traener` | IdentityService | – | – | – | yes | [#365](https://github.com/EDB-Sport/fitlife/issues/365) |
| `GET /class-sessions` | BookingService | yes | yes | yes | yes | [#254](https://github.com/EDB-Sport/fitlife/issues/254), stays open in [#369](https://github.com/EDB-Sport/fitlife/issues/369) |
| `POST /class-sessions/{id}/reservations` | BookingService | – | yes | – | – | [#260](https://github.com/EDB-Sport/fitlife/issues/260) |
| `DELETE /class-sessions/{id}/reservations/{memberId}` | BookingService | yes | yes | yes | yes | [#369](https://github.com/EDB-Sport/fitlife/issues/369) makes it Medlem only |
| `POST /class-sessions/{id}/waitlist` | BookingService | yes | yes | yes | yes | [#369](https://github.com/EDB-Sport/fitlife/issues/369) makes it Medlem only; [#263](https://github.com/EDB-Sport/fitlife/issues/263) |
| `DELETE /class-sessions/{id}/reservations/mine` (planned) | BookingService | – | yes | – | – | [#262](https://github.com/EDB-Sport/fitlife/issues/262) |
| `DELETE /class-sessions/{id}/waitlist/mine` (planned) | BookingService | – | yes | – | – | [#264](https://github.com/EDB-Sport/fitlife/issues/264) |
| `GET /reservations/mine` (planned) | BookingService | – | yes | – | – | [#265](https://github.com/EDB-Sport/fitlife/issues/265) |
| `POST /class-sessions` | BookingService | – | – | yes | yes | [#238](https://github.com/EDB-Sport/fitlife/issues/238) |
| `POST /class-sessions/{id}/cancellation` (planned) | BookingService | – | – | yes | yes | [#239](https://github.com/EDB-Sport/fitlife/issues/239) |
| `PUT /class-sessions/{id}/start` (planned) | BookingService | – | – | yes | yes | [#241](https://github.com/EDB-Sport/fitlife/issues/241) |
| `POST /members` | MembershipService | – | yes | yes | yes | [#170](https://github.com/EDB-Sport/fitlife/issues/170), any role in [#368](https://github.com/EDB-Sport/fitlife/issues/368) |
| `GET /members/me` | MembershipService | – | yes | yes | yes | [#247](https://github.com/EDB-Sport/fitlife/issues/247), any role in [#368](https://github.com/EDB-Sport/fitlife/issues/368) |
| `GET /members/by-account/{accountId}` | MembershipService | – | yes | yes | yes | [#170](https://github.com/EDB-Sport/fitlife/issues/170), any role in [#368](https://github.com/EDB-Sport/fitlife/issues/368) |
| `PUT /members/me/pause` | MembershipService | – | yes | yes | yes | [#248](https://github.com/EDB-Sport/fitlife/issues/248), any role in [#368](https://github.com/EDB-Sport/fitlife/issues/368) |
| `DELETE /members/me/pause` | MembershipService | – | yes | yes | yes | [#248](https://github.com/EDB-Sport/fitlife/issues/248), any role in [#368](https://github.com/EDB-Sport/fitlife/issues/368) |
| `POST /members/me/cancellation` (planned) | MembershipService | – | not decided | not decided | not decided | [#249](https://github.com/EDB-Sport/fitlife/issues/249) |
| `POST /members/me/payment` (planned) | MembershipService | – | not decided | not decided | not decided | [#250](https://github.com/EDB-Sport/fitlife/issues/250), [#267](https://github.com/EDB-Sport/fitlife/issues/267) |
| `GET /notifications?memberId={id}` | NotificationService | – | – | yes | yes | [#221](https://github.com/EDB-Sport/fitlife/issues/221) |
| `/mcp`, the tool `list_class_sessions` (planned) | McpServer | yes | yes | yes | yes | [#214](https://github.com/EDB-Sport/fitlife/issues/214) |
| `/mcp`, the five member tools (planned) | McpServer | – | not decided | not decided | not decided | [#215](https://github.com/EDB-Sport/fitlife/issues/215): names no role. The server only passes the member's token to Booking, whose endpoints need the role Medlem ([#260](https://github.com/EDB-Sport/fitlife/issues/260) to [#265](https://github.com/EDB-Sport/fitlife/issues/265)) |
| `/` (Home) | MemberPortal.Web | yes | yes | yes | yes | [#8](https://github.com/EDB-Sport/fitlife/issues/8) |
| `/Error` | MemberPortal.Web | yes | yes | yes | yes | [#8](https://github.com/EDB-Sport/fitlife/issues/8) |
| `/hold` (planned) | MemberPortal.Web | yes | yes | yes | yes | [#205](https://github.com/EDB-Sport/fitlife/issues/205) |
| `/log-ind` (planned) | MemberPortal.Web | yes | yes | yes | yes | [#206](https://github.com/EDB-Sport/fitlife/issues/206) |
| `/log-ud` (planned) | MemberPortal.Web | not decided | not decided | not decided | not decided | [#206](https://github.com/EDB-Sport/fitlife/issues/206) |
| `/bliv-medlem` (planned) | MemberPortal.Web | yes | yes | yes | yes | [#209](https://github.com/EDB-Sport/fitlife/issues/209) |
| `/mine-bookinger` (planned) | MemberPortal.Web | – | not decided | not decided | not decided | [#208](https://github.com/EDB-Sport/fitlife/issues/208) |
| `/mit-medlemskab` (planned) | MemberPortal.Web | – | not decided | not decided | not decided | [#210](https://github.com/EDB-Sport/fitlife/issues/210), [#211](https://github.com/EDB-Sport/fitlife/issues/211) |
| `GET /health` | every service, the portal and the gateway | yes | yes | yes | yes | – |
| The OpenAPI document and Scalar, in Development only | BookingService, IdentityService, MembershipService, NotificationService | yes | yes | yes | yes | – |
