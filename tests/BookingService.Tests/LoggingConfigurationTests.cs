using NLog.Config;
using NLog.Layouts;
using NLog.Loki;

namespace BookingService.Tests;

/// <summary>The shared nlog.config (libs/nlog.config) sends structured logs to Loki with the labels the Grafana dashboard expects.</summary>
public sealed class LoggingConfigurationTests
{
    private static LoggingConfiguration Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "nlog.config");
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
        var configuration = Load();

        // Assert
        Assert.That(configuration.LogFactory.ThrowConfigExceptions, Is.True);
    }

    [Test]
    public void LokiTarget_HasOnlyTheLabelsAppServerAndVersion()
    {
        // Arrange
        var configuration = Load();

        // Act
        var labels = LokiTarget(configuration).Labels.Select(label => label.Name);

        // Assert
        Assert.That(labels, Is.EquivalentTo(new[] { "app", "server", "version" }));
    }

    [Test]
    public void LokiTarget_WritesJsonWithEventPropertiesAndCorrelationId()
    {
        // Arrange
        var configuration = Load();

        // Act
        var layout = LokiTarget(configuration).Layout as JsonLayout;

        // Assert
        Assert.That(layout, Is.Not.Null);
        Assert.That(layout!.IncludeEventProperties, Is.True); // Justification: asserted not null on the line above
        Assert.That(layout.Attributes.Select(attribute => attribute.Name), Is.SupersetOf(new[] { "level", "message", "CorrelationId" }));
    }
}
