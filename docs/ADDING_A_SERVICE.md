# Adding a service

`src/BookingService/` is the pattern (#18). A new service is a copy of it, not a new design. One bounded context = one service = one database (`docs/METHODOLOGY.md` section 11).

Before you start, the context and its name are in `docs/domain/CONTEXT-MAP.md`. Moving a boundary is a group decision with its own issue and ADR.

## Checklist

Replace `<Service>` with the service name from the context map, e.g. `MembershipService`.

1. **Service project:** copy `src/BookingService/` to `src/<Service>/` and rename the `.csproj`. Keep:
   - `Microsoft.NET.Sdk.Web`, `net10.0`, and `<Version>0.1.0</Version>` (each service has its own version, METHODOLOGY section 2.1)
   - `Program.cs`: `AddOpenApi()`, `MapOpenApi()` and `MapScalarApiReference()` inside `IsDevelopment()` only (course rule #7), `MapHealthChecks("/health")`, and the `public partial class Program` for the tests
   - a new port in `Properties/launchSettings.json`, so two services can run side by side
   - the correlation ID: a `ProjectReference` to `libs/FitLife.Correlation/` and `app.UseMiddleware<CorrelationIdMiddleware>()` right after `Build()`. Every `IHttpClientFactory` client gets `.AddHttpMessageHandler<CorrelationIdHandler>()` (register the handler with `AddTransient<CorrelationIdHandler>()`). A RabbitMQ publisher calls `CorrelationHeaders.Write(properties.Headers)`, and a consumer handles each message inside `CorrelationContext.Begin(logger, CorrelationHeaders.Read(headers) ?? Guid.NewGuid().ToString())`
   - logging: `builder.Logging.ClearProviders()`, `builder.Host.UseNLog()` and the startup log line in `Program.cs`, inside the try/catch/finally with `LogManager.Shutdown()`. The `.csproj` links the shared `libs/nlog.config` and references `NLog.Web.AspNetCore` and `NLog.Targets.Loki`. In `appsettings.json`, set `Loki:App` to the service's compose name, e.g. `membership-svc` (that's the `app` label in Loki and Grafana). Inside compose, `Loki__Url` is `http://loki:3100`
2. **`CONTEXT.md`:** start from `docs/templates/CONTEXT.md`, with the purpose from the context map. Terms go into `GLOSSARY.md` in the same PR.
3. **Tests:** copy `tests/BookingService.Tests/` and `tests/BookingService.IntegrationTests/` to `tests/<Service>.Tests/` and `tests/<Service>.IntegrationTests/`, and update the `ProjectReference` and the namespaces.
   - `ApiDocumentationTests` (OpenAPI and Scalar return 404 in Production, 200 in Development), `HealthEndpointTests`, `StartupLogTests` and `LoggingConfigurationTests` come along. Only the service name in `StartupLogTests` changes.
   - Unit tests go in `*.Tests`. Anything with a web host, MongoDB or RabbitMQ goes in `*.IntegrationTests` (FITLIFE002).
4. **Solution:** add all three projects to `FitLife.slnx`.
5. **Packages:** versions only in `Directory.Packages.props`, never `Version=` in a `.csproj` (NU1008).
6. **Lock files:** restore with the SDK from `global.json` and commit the new `packages.lock.json` files.
7. **Build and test:** `dotnet build` and `dotnet test -c Release` from the repo root.
8. **Dockerfile:** copy `src/BookingService/Dockerfile` and replace `BookingService`. Build it from the repo root (`docker build -f src/<Service>/Dockerfile .`), and check it with `tools/ci/check-service-image.sh <Service>`: the image builds and `/health` answers 200.

Later issues add to this list as the pattern grows: the OpenAPI contract (#90), and the compose service and Nginx route (#16).
