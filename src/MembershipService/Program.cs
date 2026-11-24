using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using FitLife.Authentication;
using FitLife.Correlation;
using MembershipService.Api;
using MembershipService.Application;
using MembershipService.PaymentProvider;
using MembershipService.Repositories;
using NLog;
using NLog.Web;
using Scalar.AspNetCore;

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Course rule #8: NLog → Loki, with the shared libs/nlog.config
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();

    // The token from Identity, checked the same way in every service (#183)
    builder.Services.AddFitLifeJwtAuthentication();
    builder.Services.AddAuthorization();

    // Statuses as names in the API ("Active"), not numbers
    builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddMongoRepositories();
    builder.Services.AddOptions<MembershipFeeSettings>().BindConfiguration(MembershipFeeSettings.Section);
    builder.Services.AddPaymentProvider();
    builder.Services.AddScoped<JoinMembershipHandler>();

    WebApplication app = builder.Build();

    // First, so every log line of the request carries its X-Correlation-ID
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Course rule #7: the OpenAPI document and Scalar only in Development
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health");
    app.MapMemberEndpoints();

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
