# Adding a service

`src/BookingService/` is the pattern (#18). A new service is a copy of it, not a new design. One bounded context = one service = one database (`docs/METHODOLOGY.md` section 11).

Before you start, the context and its name are in `docs/domain/CONTEXT-MAP.md`. Moving a boundary is a group decision with its own issue and ADR.

## Checklist

Replace `<Service>` with the service name from the context map, e.g. `MembershipService`.

1. **Service project:** copy `src/BookingService/` to `src/<Service>/` and rename the `.csproj`. Keep:
   - `Microsoft.NET.Sdk.Web`, `net10.0`, and `<Version>0.1.0</Version>` (each service has its own version, METHODOLOGY section 2.1)
   - `Program.cs`: `AddOpenApi()`, `MapOpenApi()` and `MapScalarApiReference()` inside `IsDevelopment()` only (course rule #7), `MapHealthChecks("/health")`, and the `public partial class Program` for the tests
   - a new port in `Properties/launchSettings.json`, so two services can run side by side
2. **`CONTEXT.md`:** start from `docs/templates/CONTEXT.md`, with the purpose from the context map. Terms go into `GLOSSARY.md` in the same PR.
3. **Tests:** copy `tests/BookingService.Tests/` and `tests/BookingService.IntegrationTests/` to `tests/<Service>.Tests/` and `tests/<Service>.IntegrationTests/`, and update the `ProjectReference` and the namespaces.
   - `ApiDocumentationTests` (OpenAPI and Scalar return 404 in Production, 200 in Development) and `HealthEndpointTests` come along unchanged.
   - Unit tests go in `*.Tests`. Anything with a web host, MongoDB or RabbitMQ goes in `*.IntegrationTests` (FITLIFE002).
   - A unit-test project with no tests yet only prints "No test is available"; `dotnet test` still passes.
4. **Solution:** add all three projects to `FitLife.slnx`.
5. **Packages:** versions only in `Directory.Packages.props`, never `Version=` in a `.csproj` (NU1008).
6. **Lock files:** restore with the SDK from `global.json` and commit the new `packages.lock.json` files.
7. **Build and test:** `dotnet build` and `dotnet test -c Release` from the repo root.

Later issues add to this list as the pattern grows: NLog and correlation IDs (#6, #19), the Dockerfile and OpenAPI contract (#7), and the compose service and Nginx route (#16).
