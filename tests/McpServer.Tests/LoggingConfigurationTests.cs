using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Layouts;
using NLog.Loki;
using NLog.Targets;

namespace McpServer.Tests;

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
        var logEvent = new LogEventInfo(LogLevel.Error, "Mcp", "Tool called");
        logEvent.Properties["ToolName"] = "t-1";

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
        Assert.That(fields.GetProperty("message").GetString(), Is.EqualTo("Tool called"));
        Assert.That(fields.GetProperty("CorrelationId").GetString(), Is.EqualTo("c-1"));
        Assert.That(fields.GetProperty("ToolName").GetString(), Is.EqualTo("t-1"));
    }

    // Loads nlog.config with these settings, the way the host gives it its IConfiguration
    private static XmlLoggingConfiguration LoadWith(Dictionary<string, string?> settings, LogFactory logFactory)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        logFactory.Setup().SetupExtensions(extensions => extensions.RegisterConfigSettings(configuration));
        return new XmlLoggingConfiguration(Path.Combine(AppContext.BaseDirectory, "nlog.config"), logFactory);
    }

    // True when a rule sends an Info line of this logger to the Loki target
    private static bool LokiGetsInfoFrom(LoggingConfiguration configuration, string loggerName)
    {
        LokiTarget loki = LokiTarget(configuration);
        foreach (LoggingRule rule in configuration.LoggingRules)
        {
            if (!rule.NameMatches(loggerName) || !rule.IsLoggingEnabledForLevel(LogLevel.Info))
            {
                continue;
            }

            if (rule.Targets.Contains(loki))
            {
                return true;
            }

            if (rule.Final)
            {
                return false;
            }
        }

        return false;
    }

    [TestCase("McpServer")]
    [TestCase("Microsoft.Hosting.Lifetime")]
    public void LokiTarget_WithoutLokiMinLevel_GetsInfoLines(string loggerName)
    {
        // Arrange
        using var logFactory = new LogFactory();

        // Act
        XmlLoggingConfiguration configuration = LoadWith(new(StringComparer.Ordinal), logFactory);

        // Assert
        Assert.That(LokiGetsInfoFrom(configuration, loggerName), Is.True);
    }

    [TestCase("McpServer")]
    [TestCase("Microsoft.Hosting.Lifetime")]
    public void LokiTarget_WithLokiMinLevelOff_GetsNoLines(string loggerName)
    {
        // Arrange
        using var logFactory = new LogFactory();

        // Act
        XmlLoggingConfiguration configuration = LoadWith(new(StringComparer.Ordinal) { ["Loki:MinLevel"] = "Off" }, logFactory);

        // Assert
        Assert.That(LokiGetsInfoFrom(configuration, loggerName), Is.False);
    }

    [TestCase("McpServer")]
    [TestCase("Microsoft.Hosting.Lifetime")]
    public void ConsoleTarget_WithLokiMinLevelOff_StillGetsInfoLines(string loggerName)
    {
        // Arrange
        using var logFactory = new LogFactory();
        XmlLoggingConfiguration configuration = LoadWith(new(StringComparer.Ordinal) { ["Loki:MinLevel"] = "Off" }, logFactory);
        // async="true" wraps the console target, so the rules hold the wrapper under the name "console"
        Target console = configuration.AllTargets.Single(target => string.Equals(target.Name, "console", StringComparison.Ordinal));

        // Act
        bool consoleGetsInfo = configuration.LoggingRules
            .Where(rule => rule.NameMatches(loggerName) && rule.IsLoggingEnabledForLevel(LogLevel.Info))
            .Any(rule => rule.Targets.Contains(console));

        // Assert
        Assert.That(consoleGetsInfo, Is.True);
    }
}
