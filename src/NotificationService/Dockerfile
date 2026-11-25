# NotificationService image (docs/CSHARP_SETUP_TODO.md section 8). Build from the repo root:
#   docker build -f src/NotificationService/Dockerfile .
# A new service copies this file and replaces NotificationService (docs/ADDING_A_SERVICE.md).

# SDK patch pinned to match global.json (#47); MCR has no 10.0.203-alpine tag, so the Alpine version is in the tag
FROM mcr.microsoft.com/dotnet/sdk:10.0.203-alpine3.23 AS build
WORKDIR /app

# Restore first, from the project files only, so a code change keeps this layer cached (#357; tools/ci/check-dockerfile-restore-order.sh).
# The build props and the analyzer project come first because every project imports them.
COPY global.json Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY tools/Analyzers/Team.Analyzers/Team.Analyzers.csproj tools/Analyzers/Team.Analyzers/packages.lock.json tools/Analyzers/Team.Analyzers/
COPY libs/FitLife.Configuration/FitLife.Configuration.csproj libs/FitLife.Configuration/packages.lock.json libs/FitLife.Configuration/
COPY libs/FitLife.Correlation/FitLife.Correlation.csproj libs/FitLife.Correlation/packages.lock.json libs/FitLife.Correlation/
COPY libs/FitLife.Messaging/FitLife.Messaging.csproj libs/FitLife.Messaging/packages.lock.json libs/FitLife.Messaging/
COPY src/NotificationService/NotificationService.csproj src/NotificationService/packages.lock.json src/NotificationService/
RUN dotnet restore src/NotificationService/NotificationService.csproj --locked-mode

# The reviewed rule files, so the image is built with the same rules, package versions and analyzers as CI,
# then the shared code (correlation ID, nlog.config) and the service
COPY .editorconfig .globalconfig BannedSymbols.txt SonarLint.xml GLOSSARY.md ./
COPY tools/Analyzers/ tools/Analyzers/
COPY libs/ libs/
COPY src/NotificationService/ src/NotificationService/

RUN dotnet publish src/NotificationService/NotificationService.csproj -c Release -o /app/publish --no-restore

# The runtime from the pinned SDK
FROM mcr.microsoft.com/dotnet/aspnet:10.0.7-alpine3.23 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Azure overrides the port with ASPNETCORE_HTTP_PORTS (section 9a)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "NotificationService.dll"]
