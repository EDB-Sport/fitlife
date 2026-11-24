# ADR 004: Pinned image versions instead of `latest`

- **Status:** proposed
- **Date:** 2026-11-23
- **Deciders:** to be confirmed by the group (issue #14)

## Context

The IT-arkitektur course examples pull images as `latest` (`mongo`, `rabbitmq`, `nginx`, `grafana/loki`, `grafana/grafana`). This project runs the same images in three places: on each member's laptop, in CI on GitHub's Ubuntu runners, and in production on Azure Container Instances (ADR 002). Course rule #11 says everything is versioned, and the project uses Semantic Versioning for its own services (`METHODOLOGY.md` section 2.1).

`latest` is whatever the registry points at when the image is pulled. Two pulls a day apart, or two machines, can get different versions, so the same commit can behave differently in CI and in Azure without any change in the repo.

This has already happened in this project in a related way: MongoDB 8 refuses to start on Linux kernels 6.19 to 7.0.13 (MongoDB SERVER-121912). With a pinned version the failure was traced to one known image and one known kernel within minutes, and the workaround is a single, visible setting (`MONGO_IMAGE` in `deploy/compose/README.md`, added by #37). With `latest`, the same symptom could appear or disappear between two runs for no visible reason.

## Decision

Every image in the compose files, the Dockerfiles and the Bicep parameters is pinned to an exact version, e.g. `mongo:8.0.38`, `rabbitmq:4.4.1-management`, `nginx:1.30.7-alpine`. Never `latest`, and never a bare major such as `mongo:8`. Upgrades are ordinary PRs that change the version and pass CI.

CI enforces it (`docs/CSHARP_SETUP_TODO.md` sections 7 and 10):
- the `deploy-check` job fails on `:latest` in compose files and `main.bicepparam` (#21)
- hadolint fails on DL3007 (`FROM ...:latest`) in Dockerfiles (#20)

The one exception is the .NET SDK and runtime base images, which follow the course's floating `mcr.microsoft.com/dotnet/sdk:10.0-alpine` tag. This exception is not free. The Web SDK writes an implicit `Microsoft.AspNetCore.App.Internal.Assets` version into `packages.lock.json`, and that version follows the installed SDK patch. With `dotnet restore --locked-mode` (`docs/CSHARP_SETUP_TODO.md` section 1), a lock file made with one SDK patch fails with NU1004 on another, whether the newer patch comes from the image or from a laptop. `rollForward: latestFeature` in `global.json` does not prevent this: it is what lets the newer SDK in.

#47 tracks the fix the group chose: pin the exact SDK patch in `global.json` (`10.0.203` with `rollForward: disable`, PR #55) and use the matching SDK image `sdk:10.0.203-alpine3.23` in the Dockerfiles' build stage (PR #32). Once both are merged, only the runtime image floats. It does not restore packages, so the lock files do not depend on it.

## Alternatives considered

- **`latest`, as in the course.** Least work, and always the newest fixes. Rejected: builds are not reproducible, CI and Azure can run different versions, and a breaking upstream release arrives unannounced in the middle of a demo.
- **Major or minor tags only (`mongo:8`, `nginx:1.30`).** Fewer surprises than `latest`, and patches arrive automatically. Rejected: still not reproducible, and the MongoDB case above shows that even patch-level behaviour can depend on the host.
- **Pinning by digest (`mongo@sha256:...`).** Fully immutable. Rejected for now: unreadable in review and in the exam, and the tag is enough for a proof of concept.

## Consequences

- Easier: the same commit runs the same images on every laptop, in CI and in Azure. A failure points at one known version, and an upgrade shows up as a one-line diff that CI tests.
- Harder: someone must upgrade versions deliberately, including for security fixes. Trivy on the release images (#7) shows when a pinned version has known vulnerabilities.
- Harder: until #47 is done, the floating SDK image and `rollForward: latestFeature` mean that a new SDK patch on MCR or on a laptop can make `--locked-mode` restore fail with NU1004. After the pin, every member must install that exact SDK patch, and moving to a newer patch is a reviewed PR that changes `global.json` and the Dockerfile SDK tag together and regenerates the Web SDK lock files.
- Must do: keep the `:latest` check (#21) and hadolint DL3007 (#20) as required checks, and pin every new image when it is added.
