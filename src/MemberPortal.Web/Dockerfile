# Multi-stage Dockerfile for MemberPortal.Web (.NET 10 on Alpine)
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /app

# Copy project files and restore dependencies
COPY Directory.Packages.props .
COPY src/MemberPortal.Web/MemberPortal.Web.csproj src/MemberPortal.Web/
RUN dotnet restore src/MemberPortal.Web/MemberPortal.Web.csproj

# Copy remaining source code and publish
COPY src/MemberPortal.Web/ src/MemberPortal.Web/
WORKDIR /app/src/MemberPortal.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MemberPortal.Web.dll"]
