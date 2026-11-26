using System.Globalization;
using System.Reflection;
using FitLife.Authentication;
using FitLife.Configuration;
using FitLife.Correlation;
using IdentityService.Api;
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

    builder.Services.AddOpenApi(options =>
    {
        // The token requirement in the contract (#364)
        options.AddDocumentTransformer<BearerSecurityTransformer>();
        options.AddOperationTransformer<BearerSecurityTransformer>();
    });
    builder.Services.AddHealthChecks();

    // The token Identity issues, checked the same way as in every service (#183), and closed by default (#364):
    // an endpoint needs a token unless it is marked AllowAnonymous()
    builder.Services.AddFitLifeJwtAuthentication();
    builder.Services.AddFitLifeAuthorization();
    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddMongoAccountRepository(); // nothing connects until the first request
    builder.Services.AddSingleton<IPasswordHasher<Account>, PasswordHasher<Account>>();
    builder.Services.AddScoped<CreateAccountHandler>();
    builder.Services.AddScoped<ListAccountsHandler>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<TokenWriter>(); // reads Jwt:SigningKey when it writes a token, so the app starts without one
    builder.Services.AddScoped<LogInHandler>();
    builder.Services.AddSingleton<DemoAccountSeeder>();
    builder.Services.AddSingleton(provider => new Lazy<DemoAccountSeeder>(provider.GetRequiredService<DemoAccountSeeder>));
    builder.Services.AddSingleton<DemoAccountSeedStatus>();
    builder.Services.AddHostedService<DemoAccountSeedService>(); // only when Seed:DemoAccounts is true (#237)
    builder.Services.AddHealthChecks().AddCheck<DemoAccountSeedHealthCheck>("demo-accounts");

    WebApplication app = builder.Build();

    // First, so every log line of the request carries its X-Correlation-ID
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Course rule #7: the OpenAPI document and Scalar only in Development
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference().AllowAnonymous();
    }

    app.UseExceptionHandler(); // an unexpected error is a problem-details 500, without the exception's text
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapControllers();

    // A path with no endpoint answers 404, not 401: the fallback policy of AddFitLifeAuthorization() also covers
    // requests that match no endpoint, e.g. /openapi/v1.json outside Development (#440)
    app.MapFitLifeNotFoundFallback();

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
