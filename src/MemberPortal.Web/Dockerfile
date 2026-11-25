# Multi-stage Dockerfile for MemberPortal.Web (.NET 10 on Alpine)
# SDK patch pinned to match global.json (#47); MCR has no 10.0.203-alpine tag, so the Alpine version is in the tag
FROM mcr.microsoft.com/dotnet/sdk:10.0.203-alpine3.23 AS build
WORKDIR /app

# Copy root configuration, analyzers and build rules
COPY global.json .
COPY Directory.Build.props .
COPY Directory.Build.targets .
COPY Directory.Packages.props .
COPY BannedSymbols.txt .
COPY SonarLint.xml .
COPY .globalconfig .
COPY GLOSSARY.md .
COPY tools/Analyzers/ tools/Analyzers/

# Shared code (correlation ID, nlog.config)
COPY libs/ libs/

# Copy the project, then restore and publish. The whole folder is needed for restore: the Web SDK adds
# an implicit package (Microsoft.AspNetCore.App.Internal.Assets) only when it sees the project's assets,
# and the lock file must match. .dockerignore keeps the host's bin/ and obj/ out.
COPY src/MemberPortal.Web/ src/MemberPortal.Web/
RUN dotnet restore src/MemberPortal.Web/MemberPortal.Web.csproj --locked-mode
WORKDIR /app/src/MemberPortal.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MemberPortal.Web.dll"]
