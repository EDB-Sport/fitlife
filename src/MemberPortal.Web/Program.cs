using System.Globalization;
using System.Reflection;
using FitLife.Correlation;
using MemberPortal.Web.Components;
using NLog;
using NLog.Web;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Course rule #8: NLog → Loki, with the shared libs/nlog.config
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // First, so every log line of the request carries its X-Correlation-ID
    app.UseMiddleware<CorrelationIdMiddleware>();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.UseAntiforgery();

    app.MapHealthChecks("/health");

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // The startup line: which UI and which release is running (the version is also a Loki label)
    if (app.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
    {
        var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        app.Logger.LogInformation("Starting {Service} {Version}", app.Environment.ApplicationName, version);
    }

    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception exception) when (exception is not HostAbortedException) // WebApplicationFactory stops the host with HostAbortedException
{
    // Log it, and exit with 1 so Docker and Azure see that the UI failed
    LogManager.GetCurrentClassLogger().Fatal(exception, CultureInfo.InvariantCulture, "The member portal stopped because of an exception");
    Environment.ExitCode = 1;
}
finally
{
    LogManager.Shutdown();
}

/// <summary>
/// Entry point for MemberPortal.Web.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Program"/> class.
    /// </summary>
    protected Program() { }
}
