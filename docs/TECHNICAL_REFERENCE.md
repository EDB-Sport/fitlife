# Technical reference: what is built, and why

What the FitLife Digital proof of concept has on `develop`, and the reason for each part. It replaces the setup to-do list (in `docs/legacy/`, history only) and keeps its section numbers, so a pointer to "section N" still means the same topic. Where a README or ADR already covers a detail, this file links to it instead of repeating it.

Checked against `develop` on 27 November 2026 (#515). If the repo and this file disagree, the repo wins: fix this file in the same PR.

## 0. Course constraints and principle

- **Every line must be explainable at the oral exam.** The readability limits (sections 3 and 4) serve this.
- **Stack:** .NET 10 and ASP.NET Core Web API, Blazor Web App, NUnit with bUnit, MongoDB behind repositories, RabbitMQ, NLog → Loki → Grafana, Vault, JWT, Nginx, Postman/Newman with Postman Mock Servers, GitHub Actions → Docker Hub → Azure, Docker Compose (local and CI), Bicep and Azure Container Instances (production), Git Flow, Semantic Versioning.
- **Rules live in the compiler and in CI, not in prose.** Agents follow written rules about as often as people do.
- **Humans own the rule files** (`.github/CODEOWNERS`, section 1).
- **CI is the evidence.** "Tests pass" counts only as a green required check.

Two deliberate deviations from the course, each with an ADR: one repo for all services ([ADR 001](adr/001-monorepo.md)), and production on Azure Container Instances instead of on-prem compose ([ADR 002](adr/002-azure-container-instances.md)).

## 1. Repo foundation (one repo for all services)

One repo, so the rule files can't drift between services, there is one CODEOWNERS file and one CI pipeline, and a change across services is one PR. The cost is per-service releases inside one Git Flow (section 9) and a repo-root Docker build context (section 8).

| Path | What it holds |
|---|---|
| `src/<Service>/` | BookingService, IdentityService, MembershipService, NotificationService, McpServer; each with its Dockerfile, and a `CONTEXT.md` for the four bounded contexts |
| `src/MemberPortal.Web/` | The Blazor member portal (section 6a) |
| `libs/` | Shared code-owned libraries: `FitLife.Authentication`, `FitLife.Configuration` (Vault), `FitLife.Correlation`, `FitLife.Messaging` (RabbitMQ), and the shared `nlog.config` |
| `tests/` | Unit, integration and Postman tests: [`tests/README.md`](../tests/README.md), [`tests/postman/README.md`](../tests/postman/README.md) |
| `contracts/` | `openapi/<service>.json` (generated, committed), `events/*.schema.json`, `mcp/tools.json` |
| `deploy/compose/`, `deploy/azure/` | Local and CI stack, and production: [`deploy/compose/README.md`](../deploy/compose/README.md), [`deploy/azure/README.md`](../deploy/azure/README.md) |
| `tools/Analyzers/`, `tools/ci/`, `tools/dev/` | The TEAM analyzers (section 4), the CI scripts and their `*.test.sh`, and developer helpers |

The tdd-check (section 6) tells production code from tests by these paths.

- **One root solution**, `FitLife.slnx`: one `dotnet build` and one `dotnet test` cover everything.
- **`global.json`** pins SDK `10.0.203` with `rollForward: disable`. The Dockerfiles' build stage uses the matching `mcr.microsoft.com/dotnet/sdk:10.0.203-alpine3.23` (#47). A Web SDK project's lock file records an implicit package version that follows the SDK patch, so locked-mode restore needs the same patch everywhere.
- **`Directory.Build.props`**: `Nullable` and `ImplicitUsings` on, `RestorePackagesWithLockFile`, `NuGetAudit` with `NuGetAuditMode=all`, `ContinuousIntegrationBuild` when `CI=true`. It adds `BannedSymbols.txt`, `SonarLint.xml` and `GLOSSARY.md` as AdditionalFiles and wires in the TEAM analyzers. CI restores with `dotnet restore --locked-mode`.
- **`Directory.Build.targets`** holds the strictness settings (section 2). It is imported after each `.csproj`, so a project can't switch them off.
- **`Directory.Packages.props`**: Central Package Management. A `Version=` on a `PackageReference` is error NU1008, and the analyzers are `GlobalPackageReference`s (section 3).
- **No references between services.** Target `FitLifeCheckNoCrossServiceReferences` fails the build (`FITLIFE001`) when a project under `src/<A>/` references `src/<B>/`. Shared code goes in code-owned `libs/`.
- **One bounded context = one service = one database**, each with its own MongoDB user that reaches only that database ([METHODOLOGY](METHODOLOGY.md) section 11, `deploy/compose/mongodb/init/`). The member portal and McpServer own no database.
- **`.github/CODEOWNERS`** gives `@EDB-Sport/edb-team` the rule files: `.github/`, `global.json`, `Directory.*`, `tests/Directory.Build.*`, `.editorconfig`, `.globalconfig`, `.hadolint.yaml`, `BannedSymbols.txt`, `SonarLint.xml`, `stryker-config.json`, `dotnet-tools.json`, `tools/`, `libs/`, `deploy/azure/`, `docs/adr/`, `docs/METHODOLOGY.md`, `docs/domain/CONTEXT-MAP.md`, `docs/PR_RULES.md`, `AGENTS.md` and `CLAUDE.md`.

## 2. Compiler settings (in `Directory.Build.targets`)

| Setting | Why |
|---|---|
| `TreatWarningsAsErrors=true` | Every rule is an error or off, with nothing in between that agents learn to ignore |
| `WarningLevel=9999` | Every compiler warning wave, also future ones |
| `Features=strict` | A few extra compiler checks |
| `AnalysisLevel=latest-all` | All built-in CA rules; the ones that don't fit are off in `.globalconfig`, each with a reason |
| `EnforceCodeStyleInBuild=true` | Style rules (IDE*) fail the build, not only the editor |
| `GenerateDocumentationFile=true` | Course rule #4, XML comments on public code: CS1591 is an error in `src/`. It also lets IDE0005 fire and puts the comments in the OpenAPI documents |
| `Nullable=enable`, a fixed `NoWarn` | Set again here, so a `.csproj` can't change them |

Test projects get CS1591 and CA1707 off: test names such as `Reserve_ClassSessionFull_ReturnsConflict` need the underscores.

The root `.globalconfig` switches off, each with its reason in the file: CA2007 (`ConfigureAwait`), CA1303 (localization), CA1062 (null checks, covered by nullable), CA1848 (`LoggerMessage` delegates; CA2254 stays on), CA1515 (public types are needed for XML comments and `WebApplicationFactory<Program>`). The Blazor UI's exceptions are in the `[src/*.Web/**.cs]` section of the root `.editorconfig`. Target `FitLifeOnlyRepoGlobalConfig` keeps any other `.globalconfig` out of the build.

## 3. Analyzer packages (`GlobalPackageReference` in `Directory.Packages.props`)

- **SonarAnalyzer.CSharp**, with limits in the root `SonarLint.xml`: cognitive complexity (S3776) 10, nesting (S134) 3, parameters (S107) 5, lines per method (S138) 40, lines per file (S104) 300, operators per expression (S1067) 3. Short, flat methods are what someone can explain line by line.
- **Meziantou.Analyzer**: correctness and async rules with little noise, such as forwarding `CancellationToken`.
- **Microsoft.CodeAnalysis.BannedApiAnalyzers** with the root `BannedSymbols.txt`: `DateTime.Now`/`UtcNow` (inject `TimeProvider`), `Thread.Sleep`, `Task.Result`, `Task.Wait`, `new HttpClient()` (use `IHttpClientFactory`), `Console.WriteLine`, `Environment.GetEnvironmentVariable` (use `IConfiguration`), `Activator.CreateInstance`, `new Random()`. The text after `;` is the compiler message, so the fix comes with the error.
- **Nullable.Extended.Analyzer**: every `!` needs a justifying comment.
- **IDE0079** (unnecessary suppression) is an error, so stale suppressions don't pile up.

## 4. Project-specific Roslyn analyzers (`tools/Analyzers/`)

`Team.Analyzers` (`netstandard2.0`) and its NUnit tests, wired into every project by `Directory.Build.props`. The rules version with the repo, need no package feed, are code-owned, and are built test-first. Each message is an instruction: "Found X. Do Y in Z."

Built: TEAM001 to TEAM010, and TEAM012. The rule table, with what each one allows, is in [`tools/Analyzers/README.md`](../tools/Analyzers/README.md). In short: MongoDB only in `*.Repositories` (TEAM001), NLog API only in `Program.cs` (TEAM002), no secrets in log calls (TEAM003), every suppression names an issue (TEAM004), readability limits (TEAM005), every test asserts (TEAM006), no waiting in tests (TEAM007), `*.Models` types are in the glossary (TEAM008), controller actions have a verb and a typed result (TEAM009), no hard-coded URLs in `src/` (TEAM010), no conditional expression as a return value (TEAM012, #60).

Not built: TEAM011 (no public setters in `*.Domain`, #582). METHODOLOGY section 11.2 names it; today review checks that rule.

## 5. Formatting, and tools agents call directly

- **`dotnet format`** (course rule #3), configured by the root `.editorconfig` and `.globalconfig`. CI's `format` job runs `dotnet format --verify-no-changes --no-restore`.
- **Style options in `.globalconfig`**: `var` only when the type is apparent, braces always, block-bodied methods, no conditional expression in place of `if`/`return`, file-scoped namespaces, naming rules.
- **Fixers before hand edits:** `dotnet format style --diagnostics <ID>` and `dotnet format analyzers --diagnostics <ID>`.
- **A quiet build:** `dotnet build -v q -clp:NoSummary -tl:off`. The test and CI log filters are in [`AGENTS.md`](../AGENTS.md).
- Not planned: **SARIF output from the CI build** (`<ErrorLog>`). Warnings are errors, so every finding already fails the `build` job and shows in its log.

## 6. Tests (course rule #1: TDD with NUnit, mocks/stubs)

The test layout, the pyramid, how to run each level and how tdd-check works are in [`tests/README.md`](../tests/README.md); the Postman layout and the mock servers in [`tests/postman/README.md`](../tests/postman/README.md). What is in place:

- **NUnit** with constraint assertions, `Method_Scenario_ExpectedResult` names, Arrange–Act–Assert ([ADR 005](adr/005-nunit.md)). Hand-written fakes for simple cases, **Moq** for interactions and failures.
- **Unit tests stay unit tests:** `tests/Directory.Build.targets` fails the build (`FITLIFE002`) when a `*.Tests` project references Testcontainers, `Microsoft.AspNetCore.Mvc.Testing` or `MongoDB.Driver`.
- **`FakeTimeProvider`** (`Microsoft.Extensions.TimeProvider.Testing`) in place of the banned clock.
- **`WebApplicationFactory`** for in-process API tests. `ApiDocumentationTests` in Booking, Identity, Membership and Notification check that `/scalar/v1` and `/openapi/v1.json` answer 404 in `Production` (course rule #7).
- **Testcontainers** (`Testcontainers.MongoDb`, `Testcontainers.RabbitMq`) for integration tests against the real MongoDB and RabbitMQ.
- **Event schema tests** with JsonSchema.Net against `contracts/events/*.schema.json` (`tests/FitLife.Contracts.Tests`). Only `reservation-made` has a schema so far.
- **Postman collections run by Newman:** `services/` (identity, notification), `mocks/` (user registration, payment provider), `smoke/thin-slice` (run by `deploy.yml`), with `local`, `ci` and `azure` environments.
- **NLog config tests** (`LoggingConfigurationTests`) load `libs/nlog.config` with `throwConfigExceptions` and check the Loki target and its labels.
- **tdd-check** (`.github/workflows/tdd-check.yml`, `tools/ci/tdd-check.sh`): production changes in `src/` and `libs/` (and, in a second step, `tools/Analyzers/Team.Analyzers/`) come with tests that fail on the base and pass on the head; no test is deleted or skipped; a PR labelled `refactor` changes no test. A compile error on the base counts as red. It proves that the tests depend on the change, not that they came first: the `red:`/`green:` commits show the order. Details and limits: [`tests/README.md`](../tests/README.md#tdd-check).
- **Stryker.NET** (`tools/ci/mutation.sh`, `stryker-config.json` with `break: 60`) is wired up, but the `mutation` job is disabled: its condition is `github.event_name == 'workflow_dispatch'`, which nothing triggers (owner's call, 25 November 2026). #385 lists what re-enabling it needs.
- Not built: **`e2e/booking-flow`** (#274) and the `integration/` collections (`tests/postman/e2e/` and `integration/` are empty).

## 6a. Blazor user interfaces (lightweight)

- **One Blazor Web App, `src/MemberPortal.Web/`**, with Danish routes (for example `/log-ind`, `/bliv-medlem`). Static server-side rendering by default ([ADR 003](adr/003-blazor-render-mode.md)): the JWT never reaches browser script, it travels inside the encrypted, HttpOnly authentication cookie.
- **Typed clients through the Nginx gateway** (`src/MemberPortal.Web/Clients/`), with the portal's own records matching the committed OpenAPI contracts, and no `ProjectReference` to a service. A `BearerTokenHandler` adds the cookie's token.
- **Danish sentences, never a service's own text:** `ErrorSentences.cs` maps each rejection to a sentence.
- **bUnit tests** in `tests/MemberPortal.Web.Tests` with the clients faked; the cookie and the form posts in `tests/MemberPortal.Web.IntegrationTests`.
- **Logic in `.razor.cs` code-behind**, not in `@code` blocks, so the analyzers read it and tdd-check sees it.
- Not built, on purpose: a browser test framework (YAGNI; the UI is checked with users).

## 7. Logging and monitoring (course rule #8: NLog → Loki → Grafana)

- **NLog in `Program.cs`** with `UseNLog()`, inside try/catch/finally with `LogManager.Shutdown()`. Classes log through `ILogger<T>` with message templates (TEAM002, CA2254). Each service logs `Starting {Service} {Version}` at startup.
- **One shared [`libs/nlog.config`](../libs/nlog.config):** a Loki target with a `JsonLayout` (`includeEventProperties="true"`), `throwConfigExceptions="true"`, and the labels `app`, `server` and `version` only. Everything else, ids included, goes in the JSON body.
- **Correlation ID:** Nginx sets `X-Correlation-ID` when the request has none; `libs/FitLife.Correlation` has the middleware (into the logging scope), the `DelegatingHandler` for outgoing calls, and the RabbitMQ publisher and consumer carry it in the message header.
- **Grafana as code** in `deploy/compose/grafana/provisioning/`: the Loki datasource, the dashboards and an error-rate alert rule, reached at `/grafana/` through Nginx ([`deploy/compose/README.md`](../deploy/compose/README.md)).
- **Health endpoints:** every service and the portal map `/health`; compose uses them as health checks and Azure as probes. A RabbitMQ consumer has its own health check (`libs/FitLife.Messaging/RabbitMqConsumerHealthCheck.cs`).
- **Pinned image versions** in compose and Bicep (`grafana/loki:3.6.22`, `grafana/grafana:13.3.1`, `mongo:8.0.38`, `rabbitmq:4.4.1-management`, `nginx:1.30.7-alpine`, `hashicorp/vault:2.2.2`). `deploy-check` fails on `:latest` or a missing tag, and `tools/ci/image-versions.sh` on two versions of one image ([ADR 004](adr/004-pinned-image-versions.md)).
- Not built: **Loki and Grafana in Azure** (#277), and **semantic monitoring**, a nightly booking flow against Azure in `monitor.yml` (#24).

## 8. Contracts and Docker

- **OpenAPI generated at build time and committed** to `contracts/openapi/<service>.json` (`Microsoft.Extensions.ApiDescription.Server`). CI's `contract` job regenerates them and fails on a difference (`tools/ci/check-contracts.sh`); `MapOpenApi()` stays development-only.
- **Breaking-change check:** `tools/ci/oasdiff-breaking.sh` compares each contract with the one at the service's last `<service>/vX.Y.Z` tag.
- **Event schemas** in `contracts/events/` ([README](../contracts/events/README.md)); a PR that changes `contracts/` gets the `contract-change` label (diff-scan, section 10). Not built: the schemas for the other events (#92).
- **Multi-stage Dockerfiles**, one per service and for the portal, built with the repo root as context (`docker build -f src/<Service>/Dockerfile .`), so restore sees the central package versions and the rule files. Build stage `sdk:10.0.203-alpine3.23`; runtime `aspnet:10.0.15-alpine3.24` (the portal still has the floating `aspnet:10.0-alpine`). `tools/ci/check-dockerfile-restore-order.sh` and `check-service-image.sh` check the layout; the root `.dockerignore` keeps `bin/`, `obj/` and tests out.
- **hadolint** on every Dockerfile and **gitleaks** on every push and PR (`security.yml`). **Trivy** on the images a PR changes (`image-scan.yml`, `tools/ci/image-scan-plan.sh`) and on every release image (`release.yml`).

## 9. Git Flow and Semantic Versioning per service (course rules #2, #10 and #11)

The branching rules, merging with merge commits and the release steps are in [METHODOLOGY](METHODOLOGY.md) section 2.1. What the tools add:

- **Each service's `<Version>` lives in its `.csproj`** and is raised on the release branch. The .NET SDK derives `AssemblyVersion` and `FileVersion` from it.
- **The release gate** (`release.yml`, `tools/ci/release-gate.sh`) on PRs into `main`: every service changed since its last tag (in `src/<Service>/`, a `libs/` project it uses, or a root build file, #112) has a higher version; a hotfix raises one service's PATCH; a breaking OpenAPI change raises MAJOR.
- **On merge to `main`** (`release.yml`, `tools/ci/release-plan.sh`): for each service with an untagged version, the image is built, scanned by Trivy, pushed to Docker Hub as `X.Y.Z`, `X.Y`, `X` and `latest` (`docker/metadata-action`), and the commit is tagged `<service>/vX.Y.Z` plus `vx.y.z`. Then `deploy.yml` runs (section 9a).
- **Continuous Delivery into Azure:** a human decides by merging the release PR and approving the deploy; the pipeline does the rest.
- Not planned: **CI setting `AssemblyVersion`/`FileVersion`, and `kzrnm/get-net-sdk-project-versions-action`**. The SDK derives both from `<Version>`, and `release.yml` reads the version with `tools/ci/release-plan.sh`.
- **On push to `develop`** (`develop-images.yml`, `tools/ci/develop-plan.sh`): each service whose `:develop` image is missing or older than its source is built, scanned by Trivy and pushed to Docker Hub as `:develop` and `:sha-<short>`. `deploy/compose/compose.develop.yml` runs those images instead of building them (#340).

## 9a. Production on Azure: Bicep + Azure Container Instances

One container group on ACI in `switzerlandnorth`, described in Bicep (`deploy/azure/main.bicep`, `modules/storage.bicep`, `modules/containers.bicep`, `main.bicepparam`, `budget.bicep`), with Caddy in front for HTTPS (Let's Encrypt), then the same Nginx template as compose, the released services, the portal, MongoDB, RabbitMQ and Vault. Why ACI and one group: [ADR 002](adr/002-azure-container-instances.md). The containers, ports, probes, secrets, CPU and memory, and how to deploy: [`deploy/azure/README.md`](../deploy/azure/README.md).

`deploy.yml` runs after `release.yml` on `main`, or by hand: it waits for one approval on the `production` environment, logs in with OpenID Connect, prints `az deployment group what-if`, deploys, waits for HTTPS and runs the Newman smoke test (`tests/postman/smoke/`). `deploy/azure/topology.test.sh` and `deploy-versions.test.sh` check the Bicep in CI.

## 10. CI pipeline

Workflows in `.github/workflows/`. The `ci.yml` jobs print their first error as an annotation through `tools/ci/summarize.sh` (#133).

| Workflow | Jobs | What runs |
|---|---|---|
| `ci.yml` | `restore`, `build`, `format`, `test`, `contract`, `ci-scripts`, `build-rules`, `stack-check`, `mutation`, `diff-scan` | Locked restore; the build with every analyzer and build rule; `dotnet format`; `dotnet test` with coverage (ReportGenerator, uploaded as an artifact); the contracts and oasdiff; static checks (actionlint, shellcheck) and every `tools/ci/*.test.sh` and `tools/dev/*.test.sh`; `check-build-rules.sh` (each build rule still fails a build); `stack-check.sh` (no commits of another open PR); mutation (disabled, section 6); diff-scan |
| `tdd-check.yml` | `tdd-check` | Section 6 |
| `pr-description.yml` | `pr-description` | `tools/ci/check-pr-body.sh`: the issue link, "What changed and why", the answer about existing tests ([`docs/PR_RULES.md`](PR_RULES.md)) |
| `security.yml` | `security` | gitleaks, hadolint |
| `deploy-check.yml` | `deploy-check` | `tools/ci/deploy-check.sh`: `docker compose config`, no `:latest`, `nginx -t` on the rendered template, `gcx` lint of the Grafana files, `az bicep build` and `lint`, every `deploy/**/*.test.sh`, `image-versions.sh` |
| `api-tests.yml` | `api-tests` | `tools/ci/api-tests.sh`: Newman against the compose stack, HTML reports as artifacts |
| `image-scan.yml` | `image-scan-plan`, `image-scan` | Trivy on the images the PR changes |
| `release.yml`, `deploy.yml` | | Sections 9 and 9a |
| `azure-power.yml` | `power` | Starts or stops the production container group by hand, after one approval on `production` (#278) |
| `delivery-metrics.yml` | `delivery-metrics` | Weekly delivery metrics in the job summary (`tools/ci/delivery-metrics.sh`) |

**`tools/ci/diff-scan.sh`** looks only at lines the PR adds and fails on `ExcludeFromCodeCoverage`, a `.editorconfig` or `.globalconfig` outside the repo root, and `continue-on-error: true` or `if: false` in a workflow. It labels the PR `tests-modified` when existing lines under `tests/` change, and `contract-change` when `contracts/` changes.

Which checks are required is set in the repository's rulesets, not in a file; `gh pr checks <n> --required` lists them, and `tools/dev/review-ready.sh` reads them ([`AGENTS.md`](../AGENTS.md)).

## 11. Agent threats and countermeasures

| Threat | How it shows up | Countermeasure |
|---|---|---|
| Silencing the compiler | `#pragma warning disable`, `[SuppressMessage]`, `#nullable disable` | TEAM004 (issue reference required), IDE0079 |
| Switching rules off in a project | `<NoWarn>`, `<TreatWarningsAsErrors>false`, `<Nullable>disable` in a `.csproj` | Strictness sits in `Directory.Build.targets`, which is imported after the `.csproj` and overwrites it |
| Loosening a rule | Editing the root `.editorconfig` or `.globalconfig`, or adding a nested one in a subfolder | CODEOWNERS on the root files; diff-scan rejects nested ones |
| Hiding nulls | `!`, `null!`, `default!` | Nullable.Extended.Analyzer |
| Deleting or skipping tests | Removed or commented-out tests, `[Ignore]`, `[Explicit]`, `IgnoreReason`, `Assert.Ignore`, `Assume.That` | tdd-check steps 1–3 |
| Tests that check nothing | No assertion in a `[Test]` | TEAM006 |
| Integration tests disguised as unit tests | A `*.Tests` project starting a real database | Unit-test isolation check in `tests/Directory.Build.targets` |
| Tests written after the code | Tests that pass even without the change | tdd-check step 4 catches tests that don't depend on the change; only `red:`/`green:` commits show the order |
| Weak tests | Assertions that pass whatever the code does | Stryker threshold in a code-owned config (the job is disabled today, section 6) |
| Changing the expected value to match buggy output | An edited `Is.EqualTo(...)` in an existing test | `tests-modified` label + review rule in METHODOLOGY §3 |
| Hiding code from measurement | `[ExcludeFromCodeCoverage]`, editing Stryker's `mutate` or thresholds | diff-scan, CODEOWNERS on `stryker-config.json` |
| Swallowing exceptions to make tests pass | `catch (Exception) { }` | CA1031, Sonar S2486 and S108 |
| Over-building and complexity | Long methods, deep nesting, needless abstractions | Sonar limits + YAGNI in review (METHODOLOGY §5) |
| Shortcuts between services | A `ProjectReference` from one service to another | Build-time check in `Directory.Build.targets`; shared code only in code-owned `libs/` |
| Ad-hoc dependencies | New packages, `Version=` in a `.csproj` | Central Package Management (NU1008), lock files, CODEOWNERS on `Directory.Packages.props`, NuGet audit |
| Silent breaking API or event changes | Renamed fields, removed endpoints | Committed OpenAPI + oasdiff + release gate; event JSON Schemas + tests; `contract-change` label |
| Version numbers that lie | A changed service with no new version, MINOR after a breaking change | Release gate (section 9) |
| Type-system tricks | `dynamic`, reflection | TEAM005, BannedSymbols |
| Hard-coded URLs or connection strings | `"http://booking-svc:8080"`, `"mongodb://..."` in code | TEAM010, gitleaks |
| Weakening CI itself | Deleting a job, `continue-on-error`, `if: false` | Required checks by name, CODEOWNERS on `.github/`, diff-scan |
| Logging secrets or personal data | `LogInformation("... {Password}", ...)` | TEAM003 |
| Flaky tests from time or randomness | `DateTime.Now`, `new Random()`, `Thread.Sleep` | BannedSymbols, TEAM007, `FakeTimeProvider` |
| Merge churn from parallel agents | Formatting diffs, conflicting lock files | `dotnet format` in CI, lock files, one open PR per area (METHODOLOGY §10) |
| Hand-made changes in Azure | Settings changed in the portal, or a manual deploy of an unreleased image | Bicep is the only way in: CODEOWNERS on `/deploy/azure/`, the approved `deploy.yml`, `what-if` in the log |
| Secrets in infrastructure files | A password in `main.bicepparam` or a workflow file | `@secure()` parameters fed from GitHub secrets, gitleaks |

**What no tool can verify,** which review still owns:
- That a failing test fails for the right reason. tdd-check proves the test depends on the change, not that it tests the right thing.
- That names and structure make sense. TEAM008 and the Sonar limits keep code small and consistent, not clear.
- That every group member can explain the code at the exam.

The course mapping (section 12 of the old list) is not carried over: it stays in the old list in `docs/legacy/`, and course material is added by hand when needed.
