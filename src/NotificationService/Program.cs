using System.Globalization;
using System.Reflection;
using FitLife.Authentication;
using FitLife.Configuration;
using FitLife.Correlation;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using NLog;
using NLog.Web;
using NotificationService.Api;
using NotificationService.Repositories;
using NotificationService.Subscribers;
using Scalar.AspNetCore;

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Course rule #9: the secret/notification-svc secret from Vault (Vault:Address, Vault:Token), read through IConfiguration,
    // with Jwt:SigningKey
    await builder.Configuration.AddVaultAsync().ConfigureAwait(false);

    // Course rule #8: NLog → Loki, with the shared libs/nlog.config
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();

    // The token from Identity, checked the same way in every service (#183), and closed by default (#368)
    builder.Services.AddFitLifeJwtAuthentication();
    builder.Services.AddFitLifeAuthorization();
    builder.Services.AddSingleton(TimeProvider.System);

    // ReservationMade from Booking (#191): one notification per reservation, stored so a redelivery is skipped
    builder.Services.AddNotificationRepository(builder.Configuration);
    builder.Services.AddRabbitMq(builder.Configuration);
    builder.Services.AddScoped<IEventSubscriber<ReservationMade>, ReservationMadeSubscriber>();
    builder.Services.AddEventConsumer<ReservationMade>("notification");

    // Booking's three other events (#222): one notification per member, stored under the key the event's schema names
    builder.Services.AddScoped<IEventSubscriber<ReservationCancelled>, ReservationCancelledSubscriber>();
    builder.Services.AddEventConsumer<ReservationCancelled>("notification");
    builder.Services.AddScoped<IEventSubscriber<ClassSessionCancelled>, ClassSessionCancelledSubscriber>();
    builder.Services.AddEventConsumer<ClassSessionCancelled>("notification");
    builder.Services.AddScoped<IEventSubscriber<ClassSessionChanged>, ClassSessionChangedSubscriber>();
    builder.Services.AddEventConsumer<ClassSessionChanged>("notification");

    WebApplication app = builder.Build();

    // First, so every log line of the request carries its X-Correlation-ID
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Course rule #7: the OpenAPI document and Scalar only in Development
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference().AllowAnonymous();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    // The readiness probe: all checks, so a RabbitMQ outage keeps traffic away (#258, #302)
    app.MapHealthChecks("/health").AllowAnonymous();

    // The liveness probe: no dependency checks, so a RabbitMQ outage never restarts the container (#303)
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false }).AllowAnonymous();

    // The delivery log for staff (#221)
    app.MapNotificationEndpoints();

    // A path with no endpoint answers 404, not 401 (#440)
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
