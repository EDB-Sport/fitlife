using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Layouts;
using NLog.Loki;

namespace MemberPortal.Web.Tests;

/// <summary>The shared nlog.config (libs/nlog.config) sends the portal's structured logs to Loki with the labels the Grafana dashboard expects (#399).</summary>
public sealed class LoggingConfigurationTests
{
    private static readonly string[] LokiLabels = ["app", "server", "version"];

    private static XmlLoggingConfiguration Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "nlog.config");
        return new XmlLoggingConfiguration(path);
    }

    private static LokiTarget LokiTarget(LoggingConfiguration configuration)
    {
        return configuration.AllTargets.OfType<LokiTarget>().Single();
    }

    [Test]
    public void Load_ThrowsOnConfigErrors()
    {
        // Arrange & Act
        XmlLoggingConfiguration configuration = Load();

        // Assert
        Assert.That(configuration.LogFactory.ThrowConfigExceptions, Is.True);
    }

    [Test]
    public void LokiTarget_HasOnlyTheLabelsAppServerAndVersion()
    {
        // Arrange
        XmlLoggingConfiguration configuration = Load();

        // Act
        IEnumerable<string> labels = LokiTarget(configuration).Labels.Select(label => label.Name);

        // Assert
        Assert.That(labels, Is.EquivalentTo(LokiLabels));
    }

    [Test]
    public void LokiTarget_WithThePortalsAppSettings_LabelsTheLogsMemberPortal()
    {
        // Arrange
        IConfiguration settings = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        using var logFactory = new LogFactory();
        logFactory.Setup().SetupExtensions(extensions => extensions.RegisterConfigSettings(settings));
        var configuration = new XmlLoggingConfiguration(Path.Combine(AppContext.BaseDirectory, "nlog.config"), logFactory);
        Layout app = LokiTarget(configuration).Labels.Single(label => string.Equals(label.Name, "app", StringComparison.Ordinal)).Layout;

        // Act
        string label = app.Render(LogEventInfo.CreateNullEvent());

        // Assert
        Assert.That(label, Is.EqualTo("member-portal"));
    }

    [Test]
    public void LokiTarget_WritesJsonWithLevelMessageCorrelationIdAndEventProperties()
    {
        // Arrange
        Layout layout = LokiTarget(Load()).Layout;
        var logEvent = new LogEventInfo(LogLevel.Error, "MemberPortal", "Page rendered");
        logEvent.Properties["Page"] = "p-1";

        // Act
        string line;
        using (ScopeContext.PushProperty("CorrelationId", "c-1"))
        {
            line = layout.Render(logEvent);
        }

        // Assert
        using var json = JsonDocument.Parse(line);
        JsonElement fields = json.RootElement;
        Assert.That(fields.GetProperty("level").GetString(), Is.EqualTo("Error"));
        Assert.That(fields.GetProperty("message").GetString(), Is.EqualTo("Page rendered"));
        Assert.That(fields.GetProperty("CorrelationId").GetString(), Is.EqualTo("c-1"));
        Assert.That(fields.GetProperty("Page").GetString(), Is.EqualTo("p-1"));
    }
}
