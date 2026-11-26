using System.Text.Json;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Loki;

namespace NotificationService.Tests;

/// <summary>The shared nlog.config (libs/nlog.config) sends structured logs to Loki with the labels the Grafana dashboard expects.</summary>
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
    public void LokiTarget_WritesJsonWithLevelMessageCorrelationIdAndEventProperties()
    {
        // Arrange
        Layout layout = LokiTarget(Load()).Layout;
        var logEvent = new LogEventInfo(LogLevel.Error, "Notification", "Reservation made");
        logEvent.Properties["ReservationId"] = "r-1";

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
        Assert.That(fields.GetProperty("message").GetString(), Is.EqualTo("Reservation made"));
        Assert.That(fields.GetProperty("CorrelationId").GetString(), Is.EqualTo("c-1"));
        Assert.That(fields.GetProperty("ReservationId").GetString(), Is.EqualTo("r-1"));
    }
}
