# Architecture Decision Records

One file per decision, numbered `NNN-short-title.md`, copied from [`000-template.md`](000-template.md). An ADR is added in the PR that makes the decision.

Planned first ADRs (`../CSHARP_SETUP_TODO.md`, suggested order step 1):

| ADR | Decision |
|---|---|
| 001-monorepo | All services in one repo, instead of the course's one repo per service |
| 002-azure-container-instances | Production on Azure Container Instances, one container group |
| 003-blazor-render-mode | Static and Interactive Server, not WebAssembly |
| 004–006 | Reserved by #14 |
| [007-bounded-contexts](007-bounded-contexts.md) | The bounded contexts and why the system is cut this way (issue #2, proposed, open questions) |
