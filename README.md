# FitLife Digital

Proof of concept for FitLife Digital: .NET 10 microservices with lightweight Blazor user interfaces, run in Docker (Compose locally and in CI, Azure Container Instances in production).

**Running:** https://edb-fitlife.switzerlandnorth.azurecontainer.io (production on Azure Container Instances, deployed by `deploy.yml`, see [`deploy/azure/README.md`](deploy/azure/README.md)).

## Start here

| Document | What it is |
|---|---|
| [`docs/METHODOLOGY.md`](docs/METHODOLOGY.md) | How we work: issues, Git Flow, TDD, review, CI. **Read this first.** |
| [`docs/PR_RULES.md`](docs/PR_RULES.md) | The PR rules in short. CI checks the PR description. |
| [`GLOSSARY.md`](GLOSSARY.md) | The ubiquitous language. Names in code, tests and issues come from here. |
| [`docs/domain/CONTEXT-MAP.md`](docs/domain/CONTEXT-MAP.md) | Bounded contexts and how they relate (a group decision) |
| [`docs/architecture/deployment.md`](docs/architecture/deployment.md) | Where everything runs: the compose stack, the gateway, one database per service, and what travels between them |
| [`docs/DDD_PLAN.md`](docs/DDD_PLAN.md) | The steps for getting to the domain model |
| [`docs/CSHARP_SETUP_TODO.md`](docs/CSHARP_SETUP_TODO.md) | The technical setup to-do list |
| [`docs/adr/`](docs/adr/) | Architecture Decision Records |
| [`docs/templates/`](docs/templates/) | Templates for the glossary, context map and per-service `CONTEXT.md` |

## Workflow in one line

```
issue → failing test → simplest implementation → refactor (if small) → PR into develop → CI → at least one signoff → merge
```

- Branch from `develop`: `git flow feature start <issue>-<name>`, e.g. `feature/12-reserve-class-session`.
- Open the PR against `develop` with `Closes #<issue>`. Finish through the PR, not `git flow feature finish`.
- Nobody pushes directly to `main` or `develop`.

## Build rules

The build is strict on purpose: warnings are errors, all analyzers are on, and a `.csproj` can't switch the rules off. The rule files in the repo root (`Directory.Build.*`, `Directory.Packages.props`, `.editorconfig`, `.globalconfig`, `SonarLint.xml`, `BannedSymbols.txt`) are code-owned. Background: `docs/CSHARP_SETUP_TODO.md` sections 1–3.

- Build quietly: `dotnet build -v q -clp:NoSummary -tl:off`
- On an analyzer error, run the fixer before you edit by hand: `dotnet format style --diagnostics <ID>` or `dotnet format analyzers --diagnostics <ID>`. The fixes are deterministic and solution-wide.
- Check the formatting (CI will run this, see #5): `dotnet format --verify-no-changes --no-restore`
- Add a package: a `PackageVersion` in `Directory.Packages.props`, and a `PackageReference` without `Version` in the `.csproj`
- Check that the rules still work after changing a rule file: `tools/ci/check-build-rules.sh`
- Check that the code style rules still work after changing `.editorconfig` or `.globalconfig`: `tools/ci/check-style-rules.sh`
