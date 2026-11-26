using System.Globalization;
using System.Reflection;
using BookingService.Application;
using BookingService.Clients;
using BookingService.Repositories;
using BookingService.Seed;
using FitLife.Authentication;
using FitLife.Configuration;
using FitLife.Correlation;
using NLog;
using NLog.Web;
using Scalar.AspNetCore;

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Course rule #9: the secret/booking-svc secret from Vault (Vault:Address, Vault:Token), read through IConfiguration
    await builder.Configuration.AddVaultAsync().ConfigureAwait(false);

    // Course rule #8: NLog → Loki, with the shared libs/nlog.config
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();
    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddRepositories();
    builder.Services.AddScoped<GetClassSessionsQueryHandler>(); // the class schedule (#254)
    if (builder.Configuration.GetValue<bool>("Seed:Enabled"))
    {
        // The seed runs in the background; /health is unhealthy until it has finished (#255)
        builder.Services.AddSingleton<ClassSessionSeedStatus>();
        builder.Services.AddHostedService<ClassSessionSeeder>();
        builder.Services.AddHealthChecks().AddCheck<ClassSessionSeedHealthCheck>("demo-data");
    }

    // Who the member behind an account is, from Membership (#256). The reservation endpoints use it (#260)
    builder.Services.AddMembershipClient();
    builder.Services.AddScoped<ReserveSeatCommandHandler>();

    // The member's token (#183, #260): POST reservations needs the role Medlem; the class schedule and /health stay open
    builder.Services.AddFitLifeJwtAuthentication();
    builder.Services.AddAuthorization();

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
    app.UseAuthentication();
    app.UseAuthorization();
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
