using System.Globalization;
using System.Reflection;
using FitLife.Correlation;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using NLog;
using NLog.Web;
using NotificationService.Repositories;
using NotificationService.Subscribers;
using Scalar.AspNetCore;

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Course rule #8: NLog → Loki, with the shared libs/nlog.config
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();

    // ReservationMade from Booking (#191): one notification per reservation, stored so a redelivery is skipped
    builder.Services.AddNotificationRepository(builder.Configuration);
    builder.Services.AddRabbitMq(builder.Configuration);
    builder.Services.AddScoped<IEventSubscriber<ReservationMade>, ReservationMadeSubscriber>();
    builder.Services.AddEventConsumer<ReservationMade>("notification");

    WebApplication app = builder.Build();

    // First, so every log line of the request carries its X-Correlation-ID
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Course rule #7: the OpenAPI document and Scalar only in Development
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.MapHealthChecks("/health");

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
