# Architecture Decision Records

One file per decision, numbered `NNN-short-title.md`, copied from [`000-template.md`](000-template.md). An ADR is added in the PR that makes the decision.

Planned first ADRs (`../CSHARP_SETUP_TODO.md`, suggested order step 1):

| ADR | Decision |
|---|---|
| [001-monorepo](001-monorepo.md) | All services in one repo, instead of the course's one repo per service |
| [002-azure-container-instances](002-azure-container-instances.md) | Production on Azure Container Instances, one container group |
| [003-blazor-render-mode](003-blazor-render-mode.md) | Static and Interactive Server, not WebAssembly |
| [004-pinned-image-versions](004-pinned-image-versions.md) | Pinned image versions instead of the course's `latest` (proposed) |
| [005-nunit](005-nunit.md) | NUnit (IT-arkitektur) instead of Agil udvikling's MSTest (proposed) |
| [006-kanban-and-review-signoff](006-kanban-and-review-signoff.md) | Kanban flow with milestones instead of Scrum sprints, and review signoff instead of pair programming (proposed) |
