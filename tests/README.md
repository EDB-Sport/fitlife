# Tests

How tests are organised. The reasons are in `docs/CSHARP_SETUP_TODO.md` section 6 and `docs/METHODOLOGY.md` section 4.

## Layout

| Folder | What goes in it |
|---|---|
| `tests/<Service>.Tests/` | NUnit **unit tests**: one class in isolation, with stubs, fakes or Moq for the boundaries |
| `tests/<Service>.IntegrationTests/` | **Integration tests**: `WebApplicationFactory` (in-process HTTP) and Testcontainers (real MongoDB/RabbitMQ) |
| `tests/<Name>.Web.Tests/` | bUnit component tests for a Blazor UI (issue #8) |
| `tests/postman/` | Postman collections run by Newman (see `tests/postman/README.md`) |

The project name decides what kind of test project it is. `tests/Directory.Build.props` gives every project whose name ends in `.Tests` or `.IntegrationTests`:
- NUnit, the NUnit adapter and NUnit.Analyzers
- `Microsoft.NET.Test.Sdk`
- **coverlet.collector**, so `dotnet test --collect:"XPlat Code Coverage"` writes a coverage report for every test project
- `using NUnit.Framework;` as a global using

You don't add those packages yourself. Package versions live only in the root `Directory.Packages.props`.

**A unit-test project stays a unit-test project.** The build fails with `FITLIFE001` if a `*.Tests` project references `Microsoft.AspNetCore.Mvc.Testing`, `MongoDB.Driver` or any `Testcontainers` package. Those tests belong in `*.IntegrationTests`.

## Adding a test project

For a service `src/BookingService/`:

```bash
dotnet new nunit -n BookingService.Tests -o tests/BookingService.Tests
```

Then cut the `.csproj` down to this, because the packages come from `tests/Directory.Build.props`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/BookingService/BookingService.csproj" />
    <!-- Only what this project needs on top. No Version= (central package management). -->
    <PackageReference Include="Moq" />
  </ItemGroup>
</Project>
```

Delete the template's `UnitTest1.cs`, and add the project to the solution:

```bash
dotnet sln FitLife.slnx add tests/BookingService.Tests/BookingService.Tests.csproj
```

An integration-test project is the same, named `BookingService.IntegrationTests`, with `Microsoft.AspNetCore.Mvc.Testing` and/or `Testcontainers.MongoDb` / `Testcontainers.RabbitMq` instead of Moq.

## Writing tests

- Name tests `Method_Scenario_ExpectedResult`, for example `Reserve_ClassSessionFull_ReturnsConflict`.
- Arrange–Act–Assert, one behaviour per test.
- Use NUnit's constraint assertions: `Assert.That(total, Is.EqualTo(150))`.
- Hand-written stubs and fakes for simple cases; Moq (`Setup`, `Verify`, `Throws`) when you need to check calls or simulate failures. Mock boundaries (repositories, other services' clients, the clock), not domain classes.
- Use `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) instead of `DateTime.Now`.

## Running tests

```bash
dotnet test -c Release --collect:"XPlat Code Coverage"
```

Mutation testing with Stryker.NET (config in the root `stryker-config.json`, the build breaks below a 60 % mutation score):

```bash
dotnet tool restore
dotnet stryker --since:develop
```

Drop `--since:develop` to mutate everything, not just what changed since `develop`. The HTML report is written to `StrykerOutput/`.

## tdd-check

Every PR into `develop` or `main` runs `.github/workflows/tdd-check.yml`, which calls `tools/ci/tdd-check.sh`. It fails when:
1. code under `src/` (`.cs` files) changed but no test under `tests/` was added or changed
2. the PR adds a skipped test (`[Ignore]`, `[Explicit]`, `Assert.Ignore`, `Assert.Inconclusive`)
3. the PR removes tests (fewer `[Test]`/`[TestCase]`/`[TestCaseSource]`/`[Theory]` attributes than on the base)
4. the PR's tests don't pass, or they **also pass with `src/` reset to the base branch**, so they don't test the change. A test that doesn't compile against the base counts as failing, which is fine.

Run it locally before you open a PR:

```bash
BASE_SHA=$(git merge-base origin/develop HEAD) tools/ci/tdd-check.sh
```
