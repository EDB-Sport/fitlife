# ADR 003: Blazor with static rendering and Interactive Server, not WebAssembly

- **Status:** proposed
- **Date:** 2026-11-23
- **Deciders:** to be confirmed by the group (issue #1)

## Context

The project description asks for a user interface that demonstrates the implemented microservices. The stack is a Blazor Web App (`docs/CSHARP_SETUP_TODO.md` section 0), which can render on the server (static or Interactive Server) or run in the browser (WebAssembly). The UIs must log in through IdentityService with a JWT, call the services through the Nginx gateway, and stay thin: business rules belong in the services (section 6a). Every UI gets its own container, as the services do.

## Decision

Each UI is a Blazor Web App in `src/<Name>.Web/` that uses **static server-side rendering by default** and the **Interactive Server** render mode only on components that need it. The UI saves the JWT in the authentication cookie's properties. ASP.NET Core serializes those properties into the cookie, so the token does travel to the browser inside it, encrypted and signed by Data Protection. The cookie is HttpOnly, Secure and SameSite, so browser script can't read it. A `DelegatingHandler` adds the token to the typed `HttpClient`s that call the services. A server-side ticket store, which would keep the token off the browser, is not part of this decision.

## Alternatives considered

- **WebAssembly (or Interactive Auto).** Runs in the browser, works offline after download, and takes load off the server. Rejected: a larger first download and the JWT in browser storage, where a script can read it.
- **Interactive Server everywhere.** Simplest to write. Rejected: every page holds a SignalR circuit and server memory for each user, and Interactive Server components have no `HttpContext`, which makes the token handling harder on every page instead of only where interactivity is needed.

## Consequences

- Easier: one container per UI with no separate API host. The browser script never sees the token, which stays inside the encrypted HttpOnly cookie. The UI calls services the same way any other consumer of the contracts does.
- Harder: interactive components need a SignalR connection to the server, so they stop working if the connection drops. Interactive Server components can't read `HttpContext`, so a token-using component copies the token into a scoped holder when the circuit starts, or the page stays static (section 6a).
- Must do: default to static rendering and choose Interactive Server per component, with a reason in the PR. Keep logic in `.razor.cs` code-behind or small services, so the TDD check and the analyzers cover it.
