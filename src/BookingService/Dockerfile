# BookingService image (docs/CSHARP_SETUP_TODO.md section 8). Build from the repo root:
#   docker build -f src/BookingService/Dockerfile .
# A new service copies this file and replaces BookingService (docs/ADDING_A_SERVICE.md).

# SDK patch pinned to match global.json (#47); MCR has no 10.0.203-alpine tag, so the Alpine version is in the tag
FROM mcr.microsoft.com/dotnet/sdk:10.0.203-alpine3.23 AS build
WORKDIR /app

# The reviewed rule files, so the image is built with the same rules, package versions and analyzers as CI
COPY global.json Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY .editorconfig .globalconfig BannedSymbols.txt SonarLint.xml GLOSSARY.md ./
COPY tools/Analyzers/ tools/Analyzers/

# Shared code (correlation ID, nlog.config), then the service
COPY libs/ libs/
COPY src/BookingService/ src/BookingService/

RUN dotnet restore src/BookingService/BookingService.csproj --locked-mode
RUN dotnet publish src/BookingService/BookingService.csproj -c Release -o /app/publish --no-restore

# The runtime may be a newer .NET 10 patch than the SDK's (ADR 004). 10.0.15 on Alpine 3.24: 10.0.7 on
# Alpine 3.23 failed the v0.1.0 release on HIGH and CRITICAL Trivy findings, and 10.0.12 still had some.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine3.24 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Azure overrides the port with ASPNETCORE_HTTP_PORTS (section 9a)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "BookingService.dll"]
