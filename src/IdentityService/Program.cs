using System.Globalization;
using System.Reflection;
using FitLife.Configuration;
using FitLife.Correlation;
using IdentityService.Application;
using IdentityService.Domain;
using IdentityService.Repositories;
using Microsoft.AspNetCore.Identity;
using NLog;
using NLog.Web;
using Scalar.AspNetCore;

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Course rule #9: the secret/identity-svc secret from Vault (Vault:Address, Vault:Token), read through IConfiguration
    await builder.Configuration.AddVaultAsync().ConfigureAwait(false);

    // Course rule #8: NLog → Loki, with the shared libs/nlog.config
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();
    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddMongoAccountRepository(); // nothing connects until the first request
    builder.Services.AddSingleton<IPasswordHasher<Account>, PasswordHasher<Account>>();
    builder.Services.AddScoped<CreateAccountHandler>();

    WebApplication app = builder.Build();

    // First, so every log line of the request carries its X-Correlation-ID
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Course rule #7: the OpenAPI document and Scalar only in Development
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseExceptionHandler(); // an unexpected error is a problem-details 500, without the exception's text
    app.MapHealthChecks("/health");
    app.MapControllers();

    // The startup line: which service and which release is running (the version is also a Loki label)
    if (app.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
    {
        string? version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        app.Logger.LogInformation("Starting {Service} {Version}", app.Environment.ApplicationName, version);
    }

    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception exception) when (exception is not HostAbortedException) // WebApplicationFactory stops the host with HostAbortedException
{
    // Log it, and exit with 1 so Docker and Azure see that the service failed
    LogManager.GetCurrentClassLogger().Fatal(exception, CultureInfo.InvariantCulture, "The service stopped because of an exception");
    Environment.ExitCode = 1;
}
finally
{
    LogManager.Shutdown();
}

/// <summary>The entry point. Public so the integration tests can start the app with WebApplicationFactory.</summary>
public partial class Program
{
    /// <summary>Only the top-level statements create the app.</summary>
    protected Program() { }
}
