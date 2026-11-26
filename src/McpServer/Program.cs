using System.Globalization;
using System.Reflection;
using FitLife.Correlation;
using McpServer.Booking;
using McpServer.Tools;
using NLog;
using NLog.Web;

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Course rule #8: NLog → Loki, with the shared libs/nlog.config
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddHealthChecks();

    // Booking's class schedule through the gateway (#214), and the MCP SDK over HTTP with its one tool. The default session mode is stateless
    builder.Services.AddBookingClient();
    builder.Services.AddMcpServer()
        .WithHttpTransport()
        .WithTools<ClassScheduleTools>();

    WebApplication app = builder.Build();

    // First, so every log line of the request carries its X-Correlation-ID
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.MapHealthChecks("/health");
    app.MapMcp("/mcp"); // the gateway routes /mcp here (#213)

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
