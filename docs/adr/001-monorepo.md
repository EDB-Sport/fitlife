# ADR 001: All services in one repo

- **Status:** proposed
- **Date:** 2026-11-23
- **Deciders:** to be confirmed by the group (issue #1)

## Context

The IT-arkitektur course puts each microservice in its own repo, with its own pipeline and its own release. This project has several services and Blazor UIs, built by a group in which most code is written by agents (`METHODOLOGY.md`). Every service has to follow the same rules: compiler strictness, analyzers, central package versions, the TDD check and CODEOWNERS on the rule files (`docs/CSHARP_SETUP_TODO.md` sections 1–4). The project description also asks for service contracts and CI/CD into operations, so a change to a contract and its consumers has to be reviewable.

## Decision

All services, UIs, tests, contracts and deployment files live in one repo, `EDB-Sport/fitlife`, with one root solution (`FitLife.slnx`) and one CI pipeline. The services stay independent at build and release time:
- a build target fails when a project in `src/<A>/` references a project in `src/<B>/` (`docs/CSHARP_SETUP_TODO.md` section 1); services talk only through their APIs and events
- each service has its own version in its `.csproj`, its own git tag (`<service>/vX.Y.Z`) and its own image (`METHODOLOGY.md` section 2.1, `docs/CSHARP_SETUP_TODO.md` section 9)
- each service has its own database (`METHODOLOGY.md` section 11.2)

## Alternatives considered

- **One repo per service, as in the course.** Each service's history, pipeline and permissions are separate, and it's impossible to reference another service's code. Rejected: the rule files (`Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.globalconfig`, `BannedSymbols.txt`, the analyzers) would be copied into every repo and drift, CODEOWNERS and branch protection would be set up once per repo, a contract change and its consumers would be several PRs, and shared code such as the analyzers would need a private package feed.
- **One repo for the rules and analyzers, one repo per service.** Avoids copying the rules. Rejected: it still needs a package feed and a version bump in every service repo for each rule change, which is too much setup for a proof of concept with a January exam.

## Consequences

- Easier: one set of rule files that can't drift, one CODEOWNERS file and one CI pipeline. A change that spans several services is one reviewable PR, and one `dotnet build` / `dotnet test` covers the whole system.
- Harder: releases per service inside one Git Flow. A release branch carries every changed service, and the release gate has to work out per service which version must rise (`docs/CSHARP_SETUP_TODO.md` section 9). Docker builds use the repo root as context, so each Dockerfile copies the root rule files (section 8).
- Must do: keep the "no references between services" build target and the per-service versions, tags and databases. Without them, one repo turns into a distributed monolith.
