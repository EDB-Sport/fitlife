# C# setup to-do list: strict, readable, agent-proof microservices

A to-do list for the FitLife Digital proof of concept: .NET 10 microservices plus lightweight Blazor user interfaces. Everything runs in Docker: locally and in CI with Docker Compose, and in production on **Azure Container Instances, provisioned with Bicep**. The code is built test-first by several teams and mostly written by agents. Two aims:
- the code stays readable for a developer with one year of experience
- review rests on deterministic tools, not on trust

It is reconciled with:
- the project description (*F25v Projektbeskrivelse – Fase 1 og 2*): C#, microservices in containers in the cloud, monitoring, service contracts, secure identity and access, a user interface, CI/CD into operations, and the Agil udvikling test requirements (unit with coverage, integration, API, Postman MockServer, CI, end-to-end)
- the IT-arkitektur course's stack and its 13 best practices (`itarkitektur notes/coursenotes/TECH_STACK_AND_METHODS.md`)
- the Agil udvikling course (`agil udvikling notes/`)
- `METHODOLOGY.md` (Git Flow, TDD, at least one signoff)

Section 12 maps each course's requirements to where this setup meets them.

**Deliberate deviations from the course**, each recorded as an ADR, because "why did you choose X?" is an exam question:
- The course uses one repo per service. This project keeps **all services in one repo** (section 1, `docs/adr/001-monorepo.md`).
- The course deploys on-prem with compose. The project description asks for a cloud solution, so **production runs on Azure Container Instances** and compose is for local runs and CI (section 9a, `docs/adr/002-azure-container-instances.md`).

## 0. Course constraints and principle

**Course constraints that shape the setup:**
- **You must be able to explain all code, line by line, at the oral exam.** The readability limits (sections 3–4) serve this directly.
- **Stack:**
  - .NET 10, ASP.NET Core Web API
  - Blazor (Blazor Web App) for the user interfaces
  - NUnit, with bUnit for Blazor components
  - MongoDB behind repositories
  - RabbitMQ
  - NLog → Loki → Grafana
  - Vault
  - JWT
  - Nginx
  - Postman/Newman, including Postman Mock Servers
  - GitHub Actions → Docker Hub → Azure
  - Docker Compose (local and CI)
  - Azure Bicep + Azure Container Instances (production)
  - Git Flow
  - Semantic Versioning

**Principle:**
- **Rules live in the compiler and in CI**, not in prose. Agents follow written rules about as often as people do, and less often after a long session.
- **Humans own the rule files.** CODEOWNERS and branch protection mean an agent can't loosen a rule without a person seeing it.
- **CI is the evidence.** An agent saying "tests pass" counts for nothing. A green required check counts.

Each item says **what** to do and **why**.

## 1. Repo foundation (one repo for all services)

**Why one repo, against the course's one repo per service:**
- one set of rule files that can't drift between services
- one CODEOWNERS file and one CI pipeline
- a change that spans several services is one reviewable PR
- no private package feed to set up

The cost is per-service releases inside one Git Flow (section 9) and a repo-root Docker build context (section 8).

- [ ] **Layout:**

  ```
  src/<Service>/                    one folder per service, each with its own Dockerfile
  src/<Name>.Web/                   one folder per Blazor UI, with its own Dockerfile (section 6a)
  tests/<Service>.Tests/            NUnit unit tests (bUnit for tests/<Name>.Web.Tests)
  tests/<Service>.IntegrationTests/ WebApplicationFactory + Testcontainers
  tests/postman/                    Postman collections run by Newman (layout in section 6)
  contracts/openapi/<service>.json  generated OpenAPI contracts, committed
  contracts/events/*.schema.json    RabbitMQ event schemas
  deploy/compose/                   2–3 compose groups, nginx config template, Grafana provisioning (local + CI)
  deploy/azure/                     main.bicep, modules/, main.bicepparam, bicepconfig.json (production)
  docs/adr/                         Architecture Decision Records
  tools/Analyzers/                  the project's own Roslyn analyzers (section 4)
  tools/ci/                         CI scripts (diff-scan)
  ```

  *Why:* the tdd-check workflow (section 6) tells production code from tests by path. `deploy/` keeps both environments next to the code they run: compose for a laptop and CI, Bicep for Azure.
- [ ] **One root solution (`.slnx`) with every service and test project.**
  *Why:* one `dotnet build` and one `dotnet test` cover the whole system, and language servers load a single workspace.
- [ ] **`global.json`:** SDK `10.0.100` with `rollForward: latestFeature`. Dockerfiles use `mcr.microsoft.com/dotnet/sdk:10.0-alpine`, as the course does.
  *Why:* every agent and CI run use .NET 10 at or above a known baseline. The roll-forward policy keeps the course's floating `10.0-alpine` image from failing the build when its patch differs.
- [ ] **Root `Directory.Build.props`, with restore settings and defaults:**
  - `Nullable=enable`
  - `ImplicitUsings=enable`
  - `RestorePackagesWithLockFile=true`
  - `NuGetAuditMode=all`
  - `ContinuousIntegrationBuild` in CI

  CI restores with `dotnet restore --locked-mode`. Existing per-project `Nullable`/`ImplicitUsings` move here.
  *Why:* the same dependencies everywhere, every dependency change shows in the diff, and known-vulnerable packages fail the build.
- [ ] **Root `Directory.Build.targets` with the strictness settings** (section 2). It's imported **after** each `.csproj`, so a project file can't switch them off.
- [ ] **Root `Directory.Packages.props`** (Central Package Management) with the analyzers as `<GlobalPackageReference>` (section 3).
  *Why:* one version per package across services. A `Version=` on a `PackageReference` in a `.csproj` becomes error NU1008, so an agent can't add or pin a package outside the reviewed file. Every project, tests included, gets every analyzer.
- [ ] **No references between services.** A target in `Directory.Build.targets` fails the build when a project under `src/<A>/` has a `ProjectReference` into `src/<B>/`. This covers the Blazor UIs too: a UI talks to services over HTTP only.
  *Why:* in one repo it's easy to reference another service's code, and that would bypass the API and the contract. The course says to share as little as possible across services (M8, DRY with care). If a shared library is ever needed, it goes in `libs/`, under CODEOWNERS, and gets its own version.
- [ ] **One bounded context = one service = one database** (`METHODOLOGY.md` section 11, `DDD_PLAN.md`). Each service has its own MongoDB database and its own MongoDB user, which can only reach that database, even where several databases share one MongoDB container (compose and Azure). Each service folder holds a `CONTEXT.md` from `templates/CONTEXT.md`. The Blazor UIs are not bounded contexts: they own no database and no business rules.
  *Why:* a service that reads another service's collections would bypass its API and its invariants, just like a `ProjectReference`. A separate user per database makes that impossible, not just forbidden.
- [ ] **`CODEOWNERS` makes humans the owners of the rule files:**

  ```
  /global.json                  @your-org/maintainers
  /Directory.Build.props        @your-org/maintainers
  /Directory.Build.targets      @your-org/maintainers
  /Directory.Packages.props     @your-org/maintainers
  /.editorconfig                @your-org/maintainers
  /.globalconfig                @your-org/maintainers
  /BannedSymbols.txt            @your-org/maintainers
  /SonarLint.xml                @your-org/maintainers
  **/stryker-config.json        @your-org/maintainers
  /tools/                       @your-org/maintainers
  /libs/                        @your-org/maintainers
  /.github/                     @your-org/maintainers
  /deploy/azure/                @your-org/maintainers
  ```

  *Why:* the files that define "strict", and the production infrastructure, can't change without a person who owns them approving.

## 2. Compiler settings (in `Directory.Build.targets`)

- [ ] **Strictness settings**, set after the `.csproj` so they always win:

  | Setting | Why |
  |---|---|
  | `TreatWarningsAsErrors=true` | Agents treat warnings as noise. Make each rule an error or turn it off, with nothing in between. |
  | `WarningLevel=9999` | Opts into every compiler "warning wave", including future ones. |
  | `Features=strict` | A few extra compiler checks, at no cost. |
  | `AnalysisLevel=latest-all` | Turns on all built-in CA rules. Rules that don't fit are switched off in `.globalconfig`, each with a reason. |
  | `EnforceCodeStyleInBuild=true` | Style rules (IDE*) fail the build, not only the editor. |
  | `GenerateDocumentationFile=true` | **Course rule #4: XML comments on all public code.** CS1591 (missing XML comment) stays an error in `src/`. It also lets IDE0005 (unused `using`s) fire, and .NET 10 puts the XML comments into the generated OpenAPI document, so they show up in Scalar. |
  | `Nullable=enable` (again) | It can't be switched off per project. |
  | `NoWarn` set to a fixed list | It overwrites anything a `.csproj` adds, so `<NoWarn>` in a project file has no effect. |

- [ ] **Test projects** (`$(MSBuildProjectName.EndsWith('.Tests'))` or `.IntegrationTests`) get CS1591 and CA1707 off.
  *Why:* test names like `Reserve_ClassSessionFull_ReturnsConflict` document themselves and need the underscores. Rule #4 is aimed at the services' public code. If the group reads it as covering tests too, drop the CS1591 exemption.
- [ ] **The disable list in the root `.globalconfig`**, one reason per line:
  - CA2007 `ConfigureAwait`: ASP.NET Core has no synchronization context.
  - CA1303 localization: not needed.
  - CA1062 argument null checks: nullable covers them.
  - CA1848 `LoggerMessage` delegates: plain `ILogger` templates are what the course teaches, and they're more readable. CA2254, constant templates, stays on.
  - CA1515 "make public types internal": public types are needed for XML comments and `WebApplicationFactory<Program>`.
  - Blazor projects will raise a few more rules that don't fit components (for example on `[Parameter]` properties). Disable them for the UIs only, in a `[src/*.Web/**.cs]` section of the root `.editorconfig` (`.globalconfig` has no path sections), each with a reason.

  *Why:* `latest-all` without a curated list is too noisy. With one, every disabled rule is a visible, deliberate decision.

## 3. Analyzer packages (`GlobalPackageReference` in `Directory.Packages.props`)

- [ ] **SonarAnalyzer.CSharp, with limits in a root `SonarLint.xml`** (added as an AdditionalFile in `Directory.Build.props`; `.editorconfig` can't set these parameters):

  | Rule | What | Sonar default | Set to |
  |---|---|---|---|
  | S3776 | Cognitive complexity per method | 15 | **10** |
  | S134 | Nesting depth of control flow | 3 | 3 |
  | S107 | Parameters per method | 7 | **5** |
  | S138 | Lines per method | 80 | **40** |
  | S104 | Lines per file | 1000 | **300** |
  | S1067 | Operators per expression | 3 | 3 |

  ```xml
  <AnalysisInput>
    <Rules>
      <Rule>
        <Key>S3776</Key>
        <Parameters>
          <Parameter><Key>threshold</Key><Value>10</Value></Parameter>
        </Parameters>
      </Rule>
      <!-- take the parameter keys for S134, S107, S138, S104 and S1067 from the rule descriptions -->
    </Rules>
  </AnalysisInput>
  ```

  *Why:* short, flat methods with few inputs are what someone can explain line by line at the exam. Agents left alone write long, deeply nested methods. Static analysis in the delivery process is also one of Newman's security principles (M10).
- [ ] **Meziantou.Analyzer.**
  *Why:* correctness and modern-practice rules with little noise, including async rules such as forwarding `CancellationToken` (relevant for `BackgroundService` consumers).
- [ ] **Microsoft.CodeAnalysis.BannedApiAnalyzers with a root `BannedSymbols.txt`** (an AdditionalFile):

  ```
  P:System.DateTime.Now;Inject TimeProvider instead (testable)
  P:System.DateTime.UtcNow;Inject TimeProvider instead (testable)
  M:System.Threading.Thread.Sleep(System.Int32);Use await Task.Delay with an injected TimeProvider
  P:System.Threading.Tasks.Task`1.Result;Use await
  M:System.Threading.Tasks.Task.Wait;Use await
  M:System.Net.Http.HttpClient.#ctor;Inject IHttpClientFactory or a typed client (socket exhaustion, course M5)
  M:System.Console.WriteLine(System.String);Use an injected ILogger<T>
  M:System.Environment.GetEnvironmentVariable(System.String);Use IConfiguration (it reads environment variables too)
  M:System.Activator.CreateInstance(System.Type);No reflection: create the object directly
  M:System.Random.#ctor;Inject the source of randomness so tests are repeatable
  ```

  *Why:* each line enforces something the course teaches (`IHttpClientFactory`, `ILogger`, configuration through environment variables) or F.I.R.S.T. Repeatable. The text after `;` is the compiler message, so the agent reads the fix along with the error.
- [ ] **Nullable.Extended.Analyzer.**
  *Why:* flags every `!` (null-forgiving operator) that isn't justified by a comment. `!`, `null!` and `default!` are how agents make nullable errors disappear.
- [ ] **IDE0079 (unnecessary suppression) as an error.**
  *Why:* a suppression whose warning has gone away gets removed, so suppressions don't pile up.

## 4. Project-specific Roslyn analyzers (`tools/Analyzers/`)

- [ ] **Set up the analyzer projects:**
  - `tools/Analyzers/Team.Analyzers`: `netstandard2.0`, referencing `Microsoft.CodeAnalysis.CSharp`, with `EnforceExtendedAnalyzerRules`.
  - `tools/Analyzers/Team.Analyzers.Tests`: NUnit plus `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`. Skip the deprecated `*.XUnit` wrapper packages.
- [ ] **Wire the analyzer into every project from `Directory.Build.props`:**

  ```xml
  <ItemGroup Condition="'$(MSBuildProjectName)' != 'Team.Analyzers' And '$(MSBuildProjectName)' != 'Team.Analyzers.Tests'">
    <ProjectReference Include="$(MSBuildThisFileDirectory)tools/Analyzers/Team.Analyzers/Team.Analyzers.csproj"
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
  ```

  *Why:* the rules version with the repo, need no package feed, are code-owned (`/tools/`), and are built test-first: a snippet that should raise the diagnostic, and one that shouldn't.
- [ ] **Write each diagnostic message as an instruction to the agent**, in the form "Found X. Do Y in Z."
  *Why:* agents act on the compiler message straight away.
- [ ] **Add the rules one at a time.** Rename the `TEAM` prefix as you like:

  | ID | Rule | Why |
  |---|---|---|
  | TEAM001 | `MongoDB.Driver` types may only be used in the `*.Repositories` namespace. | **Course MUST:** MongoDB only through repository interfaces, never from controllers. Controllers can then be tested with a stub repository. |
  | TEAM002 | No NLog API (`LogManager`, `NLog.Logger`) outside `Program.cs`. Classes inject `ILogger<T>`. | The course pattern: NLog is set up in `Program.cs` (try/catch/finally with `LogManager.Shutdown()`), and code logs through `ILogger<T>`. CA2254 then checks every template. |
  | TEAM003 | A log call whose arguments or template placeholders are named `password`, `token`, `secret`, `connectionString`, `jwt` or `cpr` (any case) is an error. | **Course: "never log secrets or personal data"** (M9). Logs go to Loki, and everyone with Grafana access can read them. |
  | TEAM004 | `#pragma warning disable` and `#nullable disable` must be followed by `// #<issue>: <reason>`. `[SuppressMessage]` must have a `Justification`. | Closes the agents' main way around strictness, and every exception is traceable to an issue. |
  | TEAM005 | No `dynamic`, no `goto`, no LINQ query syntax, no nested ternaries, and lambdas nested at most one level deep. | Readability: code you can explain line by line. |
  | TEAM006 | Every NUnit test (`[Test]`, `[TestCase]`, `[TestCaseSource]`) contains at least one assertion (`Assert.That`, `Assert.Multiple`, `Assert.ThrowsAsync`, ...) or a Moq `Verify(...)`. | A test with no assertion always passes. Agents write those when they're asked to "add tests". |
  | TEAM007 | No `Thread.Sleep` and no `Task.Delay` in test projects. Use `FakeTimeProvider`. | F.I.R.S.T. Fast and Repeatable. |
  | TEAM008 | Public types in `*.Models` must appear in the "In code" column of `GLOSSARY.md`, read as an AdditionalFile. | **Course rules #5–6:** entity names from schema.org, in English. The glossary gets a schema.org column (for FitLife, e.g. Person, ExerciseGym, Event, Reservation, ExercisePlan), and the rule makes the code follow it. |
  | TEAM009 | Controller actions need an explicit HTTP-verb attribute and a typed result (`ActionResult<T>` or typed results). | The generated OpenAPI contract stays accurate, so the breaking-change check (section 8) means something. |
  | TEAM010 | No string literals containing `http://`, `https://`, `mongodb://` or `amqp://` in `src/`. Read them from configuration. | **Course rule #13:** no hard-coded URLs, so components can be moved. It also catches connection strings that belong in Vault (rule #9). |
  | TEAM011 *(optional)* | Types in `*.Domain` have no public setters. State changes go through methods. | **Course M4.02:** no public setters, no anemic domain model. Adopt it only if the services have a separate domain model, and check first that the MongoDB driver can map your entities (constructor or class-map mapping). For simple CRUD services it adds mapping code that hurts readability. |

  Start with TEAM004 and TEAM006, which close the biggest gaps, then TEAM001 and TEAM010, which back course MUSTs.

## 5. Formatting, and tools agents call directly

- [ ] **`dotnet format` as the formatter** (course rule #3), configured by the root `.editorconfig` (whitespace) and `.globalconfig` (code style). CI runs `dotnet format --verify-no-changes --no-restore`.
  *Why:* it's the tool the course names, and there's nothing extra to explain at the exam.
- [ ] **Readability style options in `.globalconfig`:**
  - `var` only when the type is apparent
  - braces always (IDE0011)
  - block-bodied methods
  - no conditional expression in place of `if`/`return`
  - file-scoped namespaces (IDE0161)
  - naming rules (IDE1006)
  - everything that's on at severity `error`

  *Why:* explicit code reads more easily for a junior than terse code, and agents copy whatever style they find.
- [ ] **Fixers before hand edits.** Document this for everyone: on an analyzer error, run the fixer first.
  - `dotnet format style --diagnostics <ID>`
  - `dotnet format analyzers --diagnostics <ID>`

  *Why:* the fixes are deterministic, solution-wide, and cost no tokens.
- [ ] **A quiet build command:** `dotnet build -v q -clp:NoSummary -tl:off`. Add `<ErrorLog>` in CI for SARIF output.
  *Why:* every output line costs the agent context, and SARIF gives reviewers machine-readable results.
- [ ] **Personal setup recommendations** (optional, per member, as in METHODOLOGY §9):
  - **A C# language server for your agent:** `csharp-ls`, or Microsoft's Roslyn language server, for example through Claude Code's `csharp-lsp` plugin. Run it on the same side (Windows or WSL) as the .NET SDK. *Why:* go-to-definition, find-references and diagnostics after each edit.
  - **A format-on-edit hook**, if your agent tool has hooks.
  - **Block local agent edits to the CODEOWNERS files**, for faster feedback than a blocked PR.
  - **.NET 10 file-based apps** (`dotnet run query.cs`) with `MSBuildWorkspace` for one-off semantic questions.
  - **Roslyn MCP servers only as a later, read-only pilot.** *Why:* the field is young, and each one is third-party code with full access to the solution.

## 6. Tests (course rule #1: TDD with NUnit, mocks/stubs)

**The test pyramid** (Agil udvikling M7–M8) and the tool used at each level:

| Level | What it tests | Tool here |
|---|---|---|
| E2E | The booking flow through the gateway, as a member would: log in, find a class session, reserve a seat, see it, cancel it | Newman `e2e/booking-flow` collection against the compose stack (CI) and against Azure after each deploy (section 9a) |
| Integration: service ↔ service | At least two microservices working together; services not built yet are mocked | Newman `integration/` collections against the compose stack, Postman Mock Servers for the missing services |
| Integration: service ↔ infrastructure | One service with real MongoDB/RabbitMQ | Testcontainers (`*.IntegrationTests`) |
| Contract | Services still agree on request, response and event formats | Committed OpenAPI + oasdiff, event JSON Schemas |
| Component / service | One service through its endpoints, with its dependencies stubbed; one Blazor component with its services faked | `WebApplicationFactory`, per-service Newman collection; bUnit (section 6a) |
| Unit (most tests) | One class in isolation | NUnit + Moq (`*.Tests`) |

The project description's test terms map onto this as: *unit tests* = Unit, *integration tests* = service ↔ service (documented with Postman results), *API tests* = the per-service and mock collections, *end-to-end tests* = E2E.

- [ ] **NUnit**, created with `dotnet new nunit` as the IT-arkitektur course does, using NUnit's own constraint assertions: `Assert.That(total, Is.EqualTo(150))`. Name tests `Method_Scenario_ExpectedResult`, and write them Arrange–Act–Assert.
  *Why:* it's the course's framework, it reads plainly, and it needs no extra assertion library. Agil udvikling teaches MSTest but accepts NUnit; it says the choice matters less than staying consistent.
- [ ] **Test doubles:** use a hand-written stub or fake for simple cases (`class FakeClassSessionRepository : IClassSessionRepository` with an in-memory list). Use **Moq** when you need to check interactions or simulate failures:
  - `Setup(...).Returns(...)` for canned answers
  - `Verify(..., Times.Once)` / `Times.Never` to check calls
  - `.Throws(...)` to simulate a crash (→ 500)

  Mock the boundaries (repositories, other services' clients, the clock). Don't mock simple domain classes. Pin Moq through central package management, and avoid 4.20.0, the release that shipped the SponsorLink controversy.
  *Why:* Moq is what Agil udvikling teaches, and the exam report asks how you used it. Stubs stay the easiest thing to explain when no interaction needs checking.
- [ ] **Unit-test projects stay unit tests.** A target in `Directory.Build.targets` fails the build if a `*.Tests` project references Testcontainers, `Microsoft.AspNetCore.Mvc.Testing` or `MongoDB.Driver`. Those belong in `*.IntegrationTests`.
  *Why:* the course's key distinction: a test that uses a real database is an integration test, even when it lives in the unit-test project. The check keeps the bottom of the pyramid fast and Repeatable.
- [ ] **`Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`).**
  *Why:* the testable replacement for the banned `DateTime.Now`. Useful for JWT expiry, booking and cancellation deadlines, and the membership notice period (the case: one month, counted from the first day of the next month).
- [ ] **`Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`)** for in-process API tests. Include one test per service that starts the app in the `Production` environment and asserts that `/scalar/v1` and `/openapi/v1.json` return 404.
  *Why:* fast tests of real HTTP behaviour, including `[Authorize]` returning 401 without a token. The production test enforces **course rule #7: Scalar only in development**.
- [ ] **Testcontainers** for integration tests (`Testcontainers.MongoDb`, `Testcontainers.RabbitMq`).
  *Why:* real dependencies, started fresh each run, so tests are Repeatable and run on GitHub's Ubuntu runners. It's the course's own MongoDB and RabbitMQ, started from a test.
- [ ] **A JSON Schema test for each RabbitMQ event:** serialise the event class (e.g. `ReservationMade`) and validate it against `contracts/events/ReservationMade.schema.json` with JsonSchema.Net. Producer and consumer tests both use the same file.
  *Why:* the course teaches JSON Schema as the contract (M5) and requires documented event formats. The schema is that documentation, and in one repo both sides test against exactly the same file. The schemas also go in the report's appendix of technical service contracts.
- [ ] **Postman collections, run by Newman** (METHODOLOGY §7), in this layout:

  ```
  tests/postman/
    services/<service>.postman_collection.json         API tests, one per service
    integration/<a>-<b>.postman_collection.json         two or more services working together
    mocks/<system>.mock.postman_collection.json         saved examples that back a Postman Mock Server
    mocks/<system>.mock-tests.postman_collection.json   API tests against that mock server
    e2e/booking-flow.postman_collection.json            the most important flow, end to end
    environments/local|ci|azure.postman_environment.json
  ```

  - **Per-service API tests:** every endpoint gets at least one negative case (400, 401, 403, 404 or 409 as appropriate) next to the happy path. Assertions are `pm.test` scripts on status, body fields and response time.
  - **Integration collections** test the services through each other, e.g. `booking-member`: BookingService refuses a reservation for a member without an active membership, which it learns by calling MemberService.
  - **Postman Mock Servers** stand in for systems that aren't built or can't run here, such as FitLife's existing membership/payment system or a wearables API. Build the mock collection from saved examples (success *and* error examples, picked with the `x-mock-response-name` or `x-mock-response-code` header), export it to `mocks/`, and create the mock server from it. The service under test gets the mock's URL through configuration (`LegacyMembership__BaseUrl=https://<id>.mock.pstmn.io`), never in code. Keep mocks public and filled with made-up data only, so no Postman API key is needed in CI. Check the Postman plan's monthly mock-call limit, and run the mock collections on PRs rather than on every push if it gets tight.
  - **E2E `booking-flow`:** log in at IdentityService → list class sessions → reserve a seat → list own reservations → cancel. Requests are chained with environment variables (`token → sessionId → reservationId`), and the collection cleans up after itself (it cancels what it booked), so it can also run against Azure.
  - **Results as evidence:** Newman runs with the `cli`, `junit` and `htmlextra` reporters (`newman-reporter-htmlextra`). CI uploads the HTML reports as build artifacts: those are the result tables and screenshots the report asks for.

  *Why:* the project description asks for exactly these four kinds of Postman evidence (integration, API, mock server, end-to-end), and the Agil udvikling course: most real bugs live in error paths, and a wrong status code should turn the build red just like a failing unit test.
- [ ] **Stryker.NET mutation testing on PRs:** `dotnet stryker --since:origin/${{ github.base_ref }}` (usually `develop`), with `"thresholds": { "break": 60 }` in a code-owned `stryker-config.json`. Raise the threshold over time.
  *Why:* proves the tests catch changes to the code, which a coverage number alone doesn't. The project description still asks for coverage reports, so CI produces both (section 10, job 4). It also answers the course question "if you remove the rule from the model, does the test fail?" (M4.02) for every rule at once.
- [ ] **Adopt the tdd-check workflow** (`.github/workflows/tdd-check.yml`):
  - `TEST_CMD="dotnet test -c Release"`
  - `SRC_DIR=src/`, `TEST_DIR=tests/`, and a second step with `SRC_DIR=tools/Analyzers/Team.Analyzers/`, `TEST_DIR=tools/Analyzers/Team.Analyzers.Tests/`, so a new TEAM rule is checked the same way (#49)
  - `SRC_FILE_REGEX='\.cs$'` and `TEST_FILE_REGEX='\.cs$'`, so a `CONTEXT.md`, Dockerfile or `.razor` change under `src/` needs no new test, and a PR that only adds Postman collections isn't judged by `dotnet test`
  - add the NUnit markers `[[,][[:space:]]*(Ignore|Explicit)\b|\bIgnoreReason\b|Assert\.(Ignore|Inconclusive)|\bAssume\.` to `SKIP_MARKERS`
  - take `BASE_SHA` from `git rev-parse HEAD^1` (the checked-out merge commit's first parent), not from the event's base sha, which can lag behind
  - pass the PR's labels in `PR_LABELS` and re-run on `labeled`/`unlabeled`: a PR labelled `refactor` is checked by the opposite rule (METHODOLOGY §8), no file under `tests/` may change and the tests must pass
  - add `actions/setup-dotnet` with `global-json-file: global.json` as the toolchain step
  - make it a required check on `develop` **and** `main` (PRs target `develop` under Git Flow)

  *Why:* it checks mechanically that production changes come with tests, that no test is deleted or skipped (commented-out test attributes don't count), and that the PR's tests fail without the change. A test that references a type that doesn't exist yet fails to compile on the base, and that counts as red. A run on the base that stops before any test is built (no project or solution found, `MSB1003`/`MSB1009`, or a restore error `NU1xxx`: `SETUP_ERRORS` in the script) doesn't count as red, and the check fails (#49). Its fixture tests are in `tools/ci/tdd-check.test.sh`.

  *Limits:* it proves the PR's tests **depend on the change**, not that they were written first: a test written after the code also fails on base. The optional `red:`/`green:` commits (METHODOLOGY §2) are the evidence of order. Only the suite's exit code on base is checked, so one real red test lets untested production code through, and because a compile error counts as red, a PR that adds a new project under `src/` always passes the red-on-base step. Stryker (above) is meant to cover both. Merging several `[TestCase]` lines into one `[TestCaseSource]` lowers the test count, so that cleanup fails the check.
- [ ] **A test that loads each `NLog.config`** with `throwConfigExceptions` on, and asserts that the Loki target exists, the `app` label equals the service name, and only the allowed labels from section 7 are present.
  *Why:* NLog swallows config errors by default, so a typo makes logs disappear from Grafana without any error.

## 6a. Blazor user interfaces (lightweight)

The project description asks for a user interface that demonstrates the implemented microservices and selected parts of the prototype. Keep each UI thin: pages, forms and calls to the services. Business rules stay in the services.

- [ ] **One Blazor Web App per UI, in `src/<Name>.Web/`** (e.g. `src/MemberPortal.Web/`, and `src/TrainerPortal.Web/` only if a trainer journey is built), created with `dotnet new blazor`. Use static server-side rendering by default, and the **Interactive Server** render mode only on components that need it.
  *Why:* one container per UI, no separate API host, and the JWT never reaches the browser (below). WebAssembly would mean a larger download, CORS, and tokens in browser storage. Record the choice in `docs/adr/003-blazor-render-mode.md`.
- [ ] **The UI calls services through the Nginx gateway with typed clients** (`AddHttpClient<BookingClient>`, base address from configuration). The DTOs are the UI's own records, matching the committed OpenAPI contracts. No `ProjectReference` to a service (section 1).
  *Why:* the UI is a consumer of the service contracts like any future partner, which is what the contracts are meant to prove.
- [ ] **Login and tokens:** the UI posts the login form to IdentityService, keeps the returned JWT server-side in the authentication cookie's properties, and a `DelegatingHandler` adds `Authorization: Bearer` to the typed clients. Pages that need a member use `[Authorize]`. Interactive Server components have no `HttpContext`, so copy the token into a scoped token holder when the circuit starts (Microsoft's "ASP.NET Core server-side Blazor additional security scenarios" shows the pattern), or keep token-using pages in static rendering.
  *Why:* identity and access handled in one place, and the browser only holds an HTTP-only cookie.
- [ ] **bUnit component tests** in `tests/<Name>.Web.Tests` (NUnit + bUnit), with the typed clients faked through an interface or Moq. Test behaviour that matters (a full class session disables "Reserve" and says why, a failed call shows an error), not markup details.
  *Why:* the same test-first loop as the services. The TDD hook and tdd-check apply because the layout is the same (`src/<Name>.Web` ↔ `tests/<Name>.Web.Tests`).
- [ ] **Logic in `.razor.cs` code-behind or small services, not in `@code` blocks.** The TDD hook gates `.cs` files only, and code in `.cs` files is what the analyzers and Sonar limits read best.
  *Why:* markup stays easy to change during user tests and cognitive walkthroughs, and the logic stays tested.
- [ ] **What doesn't apply to UIs:** the Scalar-in-production test and OpenAPI contract generation (a UI publishes no API). Everything else does: NLog → Loki, correlation IDs, versioning, Dockerfile, Docker Hub, Azure.
- [ ] **No browser test framework** (Playwright etc.) for now. The end-to-end requirement is met by the Postman booking flow, and the UI is checked with users (Brugerinddragelse og design).
  *Why:* YAGNI. Add it as its own issue if UI regressions start to hurt.

## 7. Logging and monitoring (course rule #8: NLog → Loki → Grafana)

- [ ] **The course setup:**
  - NLog configured in `Program.cs` with `builder.Logging.ClearProviders(); builder.Host.UseNLog();`, wrapped in try/catch/finally with `LogManager.Shutdown()`
  - classes log through an injected `ILogger<T>` with message templates (`"Reservation {ReservationId} made for class session {ClassSessionId}"`), never string interpolation

  *Why:* properties arrive in Loki as fields, loggers can be faked in tests, and CA2254 and TEAM002 enforce it.
- [ ] **The Loki target uses a `JsonLayout` with `includeEventProperties="true"`**, instead of a pipe-delimited text layout.
  *Why:* **structured logging** (M9). LogQL's `| json` can filter on `ReservationId`, `level` or `CorrelationId` directly.
- [ ] **Loki labels:** the course's `app` (service name) and `server` (`${hostname}`), plus `version` (`${assembly-version:type=Informational}`). Everything else goes in the JSON body.
  *Why:* `app` and `server` are what the course uses, and `server` tells scaled instances apart (`--scale booking-svc=3` locally, or a second container of the same service in Azure). `version` ties every log line to a release (section 9). Loki indexes only labels, so ids such as member or reservation ids never become labels.
- [ ] **Correlation ID, the course way.** The course says the gateway assigns the ID and every service logs it. In practice:
  - **Nginx** adds `proxy_set_header X-Correlation-ID $request_id;` when the incoming request has no ID.
  - **Each service** has a small middleware that reads the header, or creates an ID if it's missing, and puts it in the logging scope (`${scopeproperty:CorrelationId}` in the layout).
  - **A `DelegatingHandler`** on the `IHttpClientFactory` clients passes the header on to other services.
  - **The RabbitMQ publisher** puts the ID in the message headers, and the consumer restores it.

  In one repo the middleware and handler can be one shared file under `libs/`, which is code-owned.
  *Why:* one booking can be followed from the Blazor UI across every service in Grafana, **including across RabbitMQ**, which tracing over HTTP alone doesn't reach.
- [ ] **`throwConfigExceptions="true"`** in development and CI.
  *Why:* a broken logging config stops the service loudly instead of logging nothing.
- [ ] **Grafana as code, in `deploy/compose/grafana/`:**
  - provisioning files: the Loki datasource, dashboards as JSON, and one alert rule per service for error rate
  - admin credentials from `.env` or secrets, not `admin/admin` in the compose file (in Azure: a `@secure()` Bicep parameter, section 9a)
  - the same provisioning files are used in Azure, mounted from an Azure Files share, and Grafana is reached through Nginx at `/grafana/`

  *Why:* dashboards and alerts change through PRs and review, and every developer and the production environment get the same Grafana. It's also IaC (M15) applied to monitoring.
- [ ] **Health endpoints:** every service and UI maps `/health` (`MapHealthChecks`), with checks on its MongoDB/RabbitMQ connection where it has one. Compose uses them as `healthcheck`s, and Azure as liveness and readiness probes (section 9a).
  *Why:* the project description asks for monitoring of the critical components. A probe that fails restarts the container, which is also a resilience mechanism to discuss in the report.
- [ ] **Pin image versions** (`grafana/loki:3.x.y`, `grafana/grafana:13.x.y`, `mongo:8.x`, `rabbitmq:4.x-management`, `nginx:1.x`), even though the course examples use `latest`. A CI check fails on `:latest` in compose files, and hadolint (DL3007) fails on it in Dockerfiles.
  *Why:* SemVer is about knowing exactly what runs. A new `latest` can change the environment under you between two runs.
- [ ] **Semantic monitoring:** schedule the `e2e/booking-flow` Newman collection nightly against the Azure deployment, with a dedicated test member, and alert on failure (a failed scheduled workflow notifies by e-mail).
  *Why:* the course's M9.01 case ("everything is green, but customers complain"). A synthetic business transaction catches what health checks can't.

## 8. Contracts and Docker

- [ ] **The OpenAPI contract is generated at build time and committed:** `Microsoft.Extensions.ApiDescription.Server` with `OpenApiDocumentsDirectory`, writing `contracts/openapi/<service>.json`. CI fails if a committed file is stale (`git diff --exit-code contracts/openapi/`). This is separate from `MapOpenApi()`, which stays development-only (rule #7).
  *Why:* **contract-first and service contracts** (M3). Every API change is visible in the PR diff.
- [ ] **A breaking-change check with oasdiff** on every PR, per service: `oasdiff breaking <contract at the service's last <service>/vX.Y.Z tag> contracts/openapi/<service>.json --fail-on ERR`.
  *Why:* this is Newman's **"catch accidental breaking changes early: schema comparison"** (M8), as a tool. When a break is unavoidable, the course's options apply: add `/v2/` next to `/v1/` (coexisting versions) or emulate the old interface. Either way that service's release is a MAJOR (section 9).
- [ ] **Event schemas in `contracts/events/`**, with producer and consumer tests against them (section 6). A changed schema gets the PR label `contract-change` (diff-scan).
  *Why:* the producer owns the event format (Open Host Service / Published Language, M4), and a schema change is a contract change that every consuming team should see.
- [ ] **Multi-stage Dockerfile per service and per Blazor UI, as the course teaches it** (`sdk:10.0-alpine` → `aspnet:10.0-alpine`, listening on 8080 by default; Azure overrides the port with `ASPNETCORE_HTTP_PORTS`, section 9a), **built with the repo root as context:** `docker build -f src/<Service>/Dockerfile .`.
  - The Dockerfile copies the root rule files (`global.json`, `Directory.*.props`, `Directory.Build.targets`, `.editorconfig`, `.globalconfig`, `BannedSymbols.txt`, `SonarLint.xml`, `GLOSSARY.md`), then `tools/Analyzers/`, then its own `src/<Service>/`.
  - A root `.dockerignore` excludes `bin/`, `obj/`, `tests/` and the other services.

  *Why:* with only the service folder as context, restore fails: central package versions and the lock-file settings would be missing, and the image would be built without the reviewed rules.
- [ ] **hadolint** on every Dockerfile and **Trivy** on release images.
  *Why:* consistent, linted images, and known vulnerabilities in base images, which NuGet audit doesn't see. This covers **patching** in the course's security methods (M10).
- [ ] **gitleaks** secret scan on every PR.
  *Why:* course rule #9, "secrets in Vault, never in code or committed files", checked by a tool instead of by hope.

## 9. Git Flow and Semantic Versioning per service (course rules #2, #10 and #11)

These follow `METHODOLOGY.md` §2.1, with one repo and a separate version per service.

- [ ] **Branch protection on `main` and `develop`:**
  - every CI job is a required check, by name
  - review from code owners is required
  - no direct pushes and no admin bypass

  Every PR is merged with a merge commit, never squashed.
  *Why:* if an agent deletes or renames a job, the required check goes missing and the PR stays blocked. Merge commits keep the network graph of feature, release and hotfix branches, and the optional `red:`/`green:`/`refactor:` commits (METHODOLOGY §2), both of which are evidence for the Agil udvikling report. Squashing would also break Git Flow's back-merge of release branches into `develop`.
- [ ] **Each service's version lives in its `.csproj`** (`src/<Service>/<Service>.csproj`) and is raised on the release branch, as the course (M8) and METHODOLOGY describe. `Version` is managed by the team, and CI sets `AssemblyVersion`/`FileVersion`. CI reads the version with `kzrnm/get-net-sdk-project-versions-action`, as in the course.
  *Why:* one source of truth per service, chosen by a human, in a reviewed PR.
- [ ] **One Git Flow release can carry several service releases:**
  - A `release/<x.y.z>` branch is cut from `develop`. Its name is the release number of the system as a whole.
  - Every service with changes under `src/<Service>/` since its last tag gets a higher version in its `.csproj` on that branch.
  - A `hotfix/<service>-<x.y.z>` branch from `main` raises one service's PATCH.

  *Why:* Git Flow merges the whole of `develop` into `main`, so every changed service has to be released together, or `main` would contain unreleased code.
- [ ] **A release gate on PRs into `main`.** For each service, it fails unless:
  - changes since the service's last tag mean its version has been raised
  - a hotfix raises only PATCH, for only one service
  - if oasdiff reports a breaking change against the service's last tag, its MAJOR has been raised

  *Why:* SemVer becomes a checked property, not a convention. No service can change without a new version, and a breaking API change can't ship as a MINOR.
- [ ] **On merge to `main`,** for each service whose version has no tag yet, the release workflow:
  - tags the commit `<service>/vX.Y.Z` (for example `bookingservice/v1.3.0`), plus the METHODOLOGY tag `vx.y.z` for the release as a whole
  - builds that service's image and pushes `X.Y.Z`, `X.Y`, `X` and `latest` to Docker Hub (the course's tag scheme, using `docker/metadata-action` semver patterns with `value=` set to the service's version)
  - runs Trivy

  *Why:* course rules #10 and #11. Each service's image, DLL and git tag carry the same version, and an unchanged service is never re-released.
- [ ] **On push to `develop`** (optional): push images tagged `develop` for the services that changed (path filters, e.g. `dorny/paths-filter`).
  *Why:* METHODOLOGY says `develop` is always deployable to the test environment (the compose stack). This makes that literal.
- [ ] **The version is visible at runtime:** a startup log line with the service name and version, and `version` as a Loki label (section 7).
  *Why:* you can see in Grafana which release produced an error.
- [ ] **Say how far delivery goes: Continuous Delivery, all the way into Azure.** Every merge to `main` produces versioned images on Docker Hub, and the deploy workflow then rolls them out to Azure with Bicep, after one human approval on the `production` environment (section 9a). A human decides when to release (by merging the release PR) and approves the deploy; the pipeline does everything else. Continuous Deployment would drop that approval, and this project doesn't.
  *Why:* the project description asks for a process "til leveringen i driftsmiljøet vha. CI/CD", and the Agil udvikling exam report asks how far you got and why you stopped there. The answer: delivery into production is automated; the moment of going live is a human decision, because a broken deploy is what examiners would see behind the hand-in link.

## 9a. Production on Azure: Bicep + Azure Container Instances

The project description says the solution runs container-based, with monitoring, and that a cloud solution is the preferred form of delivery. The hand-in also needs links to the running functionality. Production therefore runs on **Azure Container Instances (ACI)**, provisioned with **Bicep**, as in course module M15 (`foodfactory.bicep`). Record the topology and its trade-offs in `docs/adr/002-azure-container-instances.md`.

- [ ] **Bicep in `deploy/azure/`:**
  - `main.bicep` (scope: resource group), with modules in `modules/`: `storage.bicep` (storage account + Azure Files shares) and `containers.bicep` (the container group)
  - `main.bicepparam`: region, DNS label, and **one image tag per service**, which the deploy workflow overrides with the released versions. No secrets in it.
  - `bicepconfig.json` with the linter rules at `error`
  - `@description()` on every parameter, `@secure()` on every secret parameter, and the public URL as an `output`

  *Why:* the environment is rebuilt from the repo with one command, reviewed like code (CODEOWNERS on `/deploy/azure/`), and torn down with `az group delete` after the exam.
- [ ] **Topology: one container group, one public port.** All containers (Nginx, the services, the Blazor UIs, MongoDB, RabbitMQ, Vault, Loki, Grafana) run in one container group. Only the entry point (Nginx, or Caddy in front of it, below) has a port on the group's public IP, with a DNS label (`<label>.<region>.azurecontainer.io`). Everything else is reachable only inside the group.
  - Containers in a group share `localhost`, so each one gets its own port: `ASPNETCORE_HTTP_PORTS=5001`, `5002`, … for the .NET containers, and the services find each other through configuration (`Services__Member__BaseUrl=http://localhost:5001`). Compose uses `http://member-svc:8080` for the same setting. This is course rule #13 (movable, configurable components) in action.
  - Nginx uses one config template for both environments: the official image runs `envsubst` on `/etc/nginx/templates/*.template`, so the upstream addresses come from environment variables. Bicep mounts the same template file into the container as a secret volume (`loadFileAsBase64('../compose/nginx/default.conf.template')`), so there is one source.
  - A container group has CPU and memory limits per region. Give the .NET containers small requests (e.g. 0.25 vCPU, 0.5 GB) and check the total against the limit. If it doesn't fit, split into a second group.

  **The course's variant** (`foodfactory.bicep`): several container groups in a VNet with an **Application Gateway** in front. It's closer to a real production setup, but the Application Gateway has a fixed hourly cost that would use up most of an Azure for Students credit before the January exam. The ADR should say which you chose and why. The report's discussion of scaling and resilience can still describe the gateway variant as the next step.
- [ ] **Persistence on Azure Files** shares for MongoDB, Loki and Grafana data, as the course does. If MongoDB has trouble on the SMB share, record it and consider Azure Cosmos DB for MongoDB as the managed alternative in the ADR.
- [ ] **HTTPS on the public endpoint.** ACI only gives you an IP and a DNS name, no certificate. JWTs and passwords must not travel over plain HTTP, so put **Caddy as a sidecar** in the group (Microsoft Learn: "Enable automatic HTTPS with Caddy as a sidecar container" in ACI). Caddy gets a Let's Encrypt certificate for the DNS name and forwards to Nginx on `localhost`. Its certificate data goes on a file share, so a restart doesn't request a new certificate.
  *Why:* the project description requires correct security handling of users' data and access.
- [ ] **Secrets:** MongoDB and RabbitMQ passwords, the Grafana admin password, the Vault seed values and the Docker Hub pull token are `@secure()` parameters. The deploy workflow passes them from GitHub Actions secrets, and Bicep hands them to containers as `secureValue` environment variables, which the Azure portal doesn't show. The services read their secrets from Vault, as in the course. Say in the report that dev-mode Vault keeps secrets in memory only, and what a production setup would use instead (Vault with storage, or Azure Key Vault).
- [ ] **Images from Docker Hub** (course rule #10), pulled with `imageRegistryCredentials` (a Docker Hub access token), so anonymous pull limits can't break a deploy.
- [ ] **Probes and restarts:** `restartPolicy: Always`, plus liveness and readiness probes on each .NET container's `/health` (section 7).
- [ ] **`deploy.yml`:** runs after `release.yml` succeeds on `main`.
  1. The job uses the GitHub environment `production` with **required reviewers**: the deploy waits for one group member's approval.
  2. It logs in with `azure/login` using **OpenID Connect** (a federated credential on an app registration, so no client secret is stored).
  3. `az deployment group what-if`, printed in the log, then `az deployment group create` with the released image tags.
  4. A smoke test: Newman runs the per-service collections and `e2e/booking-flow` with `environments/azure`. A failure fails the deploy, and the fix is to re-run the workflow for the previous release's tags.

  *Why:* the delivery into operations is automated, repeatable and logged, which is the SDLC the project description asks you to show.
  **Check early:** an Azure for Students subscription in a school tenant may not let you create app registrations. If it doesn't, one member runs the same `az deployment group create` from their machine, and CI only runs the smoke test. Record that as a deviation.
- [ ] **Cost:** ACI bills per second while containers run. Set a budget alert on the subscription, keep the group running from hand-in (18 Dec) through the exam so the links work, and delete the resource group after the exam.
- [ ] **What to write in the report** (IT-arkitektur, max 8 pages): the deployment diagram (Caddy → Nginx → UIs and services → MongoDB, RabbitMQ, Vault, Loki/Grafana, all in one container group, with ports and protocols), why ACI and why one group, and the operations discussion: security (TLS, JWT, Vault, secure parameters), monitoring (Loki/Grafana, probes, nightly booking flow), scaling (no autoscaling in ACI; more instances behind Nginx or more groups behind a gateway) and resilience (restarts, the course's Nginx retry and circuit-breaker settings).

## 10. CI pipeline

**`ci.yml`**, on every PR and on every push to `develop` and `main`. Other branches get CI through their PR, so a PR head runs once, not twice with `diff-scan` skipped in the push run (GitHub reports a skipped job as passing). Each job is a required check (section 9).

| # | Job | Command or tool |
|---|---|---|
| 1 | restore | `dotnet restore --locked-mode` |
| 2 | build | `dotnet build -c Release --no-restore -v q -clp:NoSummary -tl:off`, which runs the compiler, all analyzers, the TEAM rules, the no-cross-service-reference check and the unit-test isolation check |
| 3 | format | `dotnet format --verify-no-changes --no-restore` |
| 4 | test | `dotnet test -c Release --no-build --collect:"XPlat Code Coverage"`: NUnit unit tests, bUnit component tests, `WebApplicationFactory` (including the Scalar-in-production test), Testcontainers, event-schema tests, the NLog config tests, and the analyzer tests. Coverage comes from `coverlet.collector` in every test project, and ReportGenerator turns it into an HTML report, uploaded as an artifact |
| 5 | tdd-check | section 6 |
| 6 | mutation | `dotnet stryker --since:origin/<base branch>` |
| 7 | contract | OpenAPI stale check + oasdiff per service |
| 8 | security | gitleaks, hadolint |
| 9 | deploy-check | `docker compose -f deploy/compose/<each group> config -q`, `nginx -t` on the rendered Nginx template (run in the pinned nginx image), the `:latest` check (compose files and `main.bicepparam`), linting of Grafana dashboards and alert rules with Grafana's `gcx` CLI, `az bicep build` and `az bicep lint` on `deploy/azure/main.bicep` |
| 10 | api-tests | Newman against `docker compose up --wait` from `deploy/compose/`: the `services/`, `integration/` and `e2e/` collections; the `mocks/` collections against the Postman Mock Servers. HTML reports uploaded as artifacts |
| 11 | diff-scan | `tools/ci/diff-scan.sh` (below) |
| 12 | traceability | The PR body contains `Closes #<n>`, `Fixes #<n>` or `Resolves #<n>` (a one-line `grep` on the PR body) |

- [ ] **`tools/ci/diff-scan.sh`** (about 20 lines of bash, code-owned). It looks only at lines **added** by the PR (`git diff -U0 base...head`) and fails on:
  - `[ExcludeFromCodeCoverage]`
  - a new `.editorconfig` or `.globalconfig` anywhere except the root
  - `continue-on-error: true` or `if: false` in `.github/workflows/`

  It adds two PR labels:
  - `tests-modified` when existing lines under `tests/` are removed or changed
  - `contract-change` when anything under `contracts/` changes

  *Why:* these are the text-level escapes the compiler can't see. The labels point reviewers at the riskiest kinds of change (METHODOLOGY §3).
- [ ] **`release.yml`:** the release gate on PRs into `main`, then tags, Docker Hub pushes and Trivy on merge (section 9).
- [ ] **`deploy.yml`:** after a successful `release.yml`, the approved Bicep deploy to Azure and the Newman smoke test (section 9a).
- [ ] **`monitor.yml`:** a nightly run of `e2e/booking-flow` against Azure, as semantic monitoring (section 7).

## 11. Agent threats and countermeasures

| Threat | How it shows up | Countermeasure |
|---|---|---|
| Silencing the compiler | `#pragma warning disable`, `[SuppressMessage]`, `#nullable disable` | TEAM004 (issue reference required), IDE0079 |
| Switching rules off in a project | `<NoWarn>`, `<TreatWarningsAsErrors>false`, `<Nullable>disable` in a `.csproj` | Strictness sits in `Directory.Build.targets`, which is imported after the `.csproj` and overwrites it |
| Loosening a rule | Editing the root `.editorconfig` or `.globalconfig`, or adding a nested one in a subfolder | CODEOWNERS on the root files; diff-scan rejects nested ones |
| Hiding nulls | `!`, `null!`, `default!` | Nullable.Extended.Analyzer |
| Deleting or skipping tests | Removed or commented-out tests, `[Ignore]`, `[Explicit]`, `IgnoreReason`, `Assert.Ignore`, `Assume.That` | tdd-check steps 1–3 |
| Tests that check nothing | No assertion in a `[Test]` | TEAM006 |
| Integration tests disguised as unit tests | A `*.Tests` project starting a real database | Unit-test isolation check in `Directory.Build.targets` |
| Tests written after the code | Tests that pass even without the change | tdd-check step 4 catches tests that don't depend on the change; only `red:`/`green:` commits show the order |
| Weak tests | Assertions that pass whatever the code does | Stryker threshold in a code-owned config |
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

## 12. Course requirements → where they're met

### IT-arkitektur: the course's 13 best practices (from the M12 micro-project brief)

| # | Course rule | Enforced by |
|---|---|---|
| 1 | TDD, unit-tested code (NUnit, mocks/stubs) | tdd-check, TEAM006, Stryker, NUnit + stubs (section 6) |
| 2 | Git Flow | Branch protection on `main` and `develop`, release gate (section 9) |
| 3 | Code formatted before `main`/`develop` | `dotnet format --verify-no-changes` as a required check |
| 4 | XML comments on all public code | `GenerateDocumentationFile` + CS1591 as an error in `src/` |
| 5 | Entity names from schema.org | Glossary with a schema.org column + TEAM008 |
| 6 | Classes, properties and endpoints in English | Glossary + TEAM008 for models; review for the rest |
| 7 | Scalar/Swagger only in development | `WebApplicationFactory` test in `Production` (404) |
| 8 | Solution can be monitored (NLog + Loki + Grafana) | NLog config tests, Grafana provisioning + `gcx` lint, `/health` probes, semantic monitoring against Azure |
| 9 | Secrets in Vault | gitleaks, TEAM010, TEAM003, `@secure()` Bicep parameters |
| 10 | CI pipeline → Docker Hub for every service | `release.yml`, per changed service and UI |
| 11 | Everything versioned (assemblies and images) | Version per `.csproj`, release gate, `<service>/vX.Y.Z` tag = image tags = assembly version = the tag Bicep deploys |
| 12 | On-prem Docker with compose files | **Adapted:** compose in `deploy/compose/` for local runs and CI (`compose config` check, Newman against the stack); production on Azure Container Instances with Bicep, because the project description asks for a cloud solution (section 9a, ADR 002) |
| 13 | Movable, configurable components (environment variables) | TEAM010, banned `Environment.GetEnvironmentVariable` in favour of `IConfiguration`; the same images run in compose and Azure with only environment variables changed (section 9a) |

Also from the course: MongoDB only through repositories (TEAM001), `IHttpClientFactory` (BannedSymbols), no secrets or personal data in logs (TEAM003), correlation IDs and structured logs (section 7), catching breaking changes early (oasdiff), micro frontends and UI ownership (M14: one Blazor UI per user group, section 6a), and IaC with Bicep and ACI (M15, section 9a).

### The project description's PoC requirements

| Requirement (IT-arkitektur og -infrastruktur) | Where it's met |
|---|---|
| Central components of FitLife Digital as microservices | Services cut from the target picture with DDD: `DDD_PLAN.md`, `METHODOLOGY.md` section 11 |
| Container-based, with monitoring of critical components, preferably in the cloud | Docker images, Azure Container Instances (section 9a), Loki/Grafana, `/health` probes (section 7) |
| Well-defined service contracts for future partners | Committed OpenAPI contracts, event JSON Schemas (section 8); they go in the report's appendix |
| Secure handling of identity and access | IdentityService + JWT, Vault, HTTPS via Caddy, secure Bicep parameters (sections 6a, 9a) |
| SDLC from DDD analysis to delivery into operations with CI/CD | Git Flow, CI, release and deploy workflows (sections 9, 9a, 10) |
| C#, best practice, design patterns, version control | Sections 1–5; repositories, typed clients, middleware and `DelegatingHandler` as patterns to name in the report |
| A user interface that demonstrates the services | Blazor UIs (section 6a) |
| Links to the running functionality | The Azure DNS name (`output` of `main.bicep`) |
| Code in a GitHub organisation, read access for the examiners | Create the repo in a GitHub organisation, not a personal account |

### Agil udvikling: exam report topics

| Report topic | Where this setup gives concrete examples |
|---|---|
| Scrum as project management | Not used as such: the board runs as continuous Kanban flow, with milestones per release (METHODOLOGY §2). Explain this as an adaptation of the method. |
| XP practices (pairing, CI, coding standards, TDD, refactoring) | METHODOLOGY §10; coding standards as analyzers and `dotnet format` (sections 2–5); CI (section 10) |
| Testing, with experience of automated tests | Test pyramid (section 6), NUnit |
| Use of Moq | `Setup`/`Returns`, `Verify`/`Times.Never` and `Throws` in unit tests (section 6) |
| TDD and refactoring, with concrete examples | tdd-check, optional `red:`/`green:`/`refactor:` commits, refactoring issues (METHODOLOGY §8) |
| Continuous integration, delivery, deployment | CI on every push and PR; Continuous Delivery into Azure with one approval step, so not Continuous Deployment (sections 9, 9a) |
| Postman and JavaScript test scripts | Newman collections with `pm.test`, chained variables and negative cases; integration, mock-server and end-to-end collections (section 6) |
| Choice of method and adaptation | The deviations below, plus pairing replaced by review (METHODOLOGY §10) |

Evidence to collect along the way:
- board screenshots
- a PR with real review comments
- the network graph showing feature, release and hotfix branches
- one red → green → refactor sequence
- a Moq test
- a CI run that's red, then green
- a PR blocked by a failing check
- coverage reports and test-runner output from CI (job 4)
- Newman HTML reports for the API, integration, mock-server and end-to-end collections (job 10)
- a `deploy.yml` run with its approval, `what-if` output and smoke test

### Deliberate deviations, each worth an ADR
- one repo for all services, instead of the IT-arkitektur course's one repo per service (section 1)
- Azure Container Instances for production instead of the course's on-prem compose, and one container group instead of the course's Application Gateway variant (section 9a)
- Blazor render mode: static and Interactive Server rather than WebAssembly (section 6a)
- pinned image versions instead of `latest` (section 7)
- NUnit instead of the Agil udvikling course's MSTest (section 6)
- Kanban flow instead of Scrum sprints, and review signoff instead of pair programming (METHODOLOGY §10)

## Suggested order

1. Write ADRs 001–003 (one repo, Azure topology, Blazor render mode). **Check straight away** that your Azure for Students subscription can create an app registration for OIDC (section 9a).
2. Section 1, then sections 2–3. Turn on `latest-all` and work through the first batch of errors with the fixers, disabling rules only with a reason.
3. Section 6: tests, tdd-check, Stryker, the Postman layout. Set up the first Postman Mock Server early, so services can be built against it.
4. Section 10: CI and branch protection on `main` and `develop`.
5. Section 7: logging, correlation IDs, health endpoints, Grafana as code.
6. Sections 8–9: contracts, Docker build context, release gate, Docker Hub release.
7. Section 9a: a first Bicep deploy of a thin slice (Caddy, Nginx, one service, one UI) early, well before 18 Dec, then grow it with each release.
8. Section 6a: the Blazor UI, in step with the services it demonstrates.
9. Section 4: TEAM analyzers, one rule at a time, starting with TEAM004, TEAM006, TEAM001 and TEAM010.
