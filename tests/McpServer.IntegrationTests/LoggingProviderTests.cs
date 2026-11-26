using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace McpServer.IntegrationTests;

/// <summary>Course rule #8: McpServer logs through NLog only, so every line reaches Loki with the shared labels.</summary>
public sealed class LoggingProviderTests
{
    [Test]
    public void Start_UsesNLogAsTheOnlyLoggerProvider()
    {
        // Arrange
        using var factory = new WebApplicationFactory<Program>();

        // Act
        IEnumerable<ILoggerProvider> providers = factory.Services.GetServices<ILoggerProvider>();

        // Assert
        Assert.That(providers.Select(provider => provider.GetType()), Is.EqualTo(new[] { typeof(NLogLoggerProvider) }));
    }

    [Test]
    public void Start_InATestRun_HasLokiMinLevelOff()
    {
        // Arrange: no Loki answers in a test run, and the target's retries made host shutdown time out (#394).
        // tests/test.runsettings sets Loki__MinLevel; McpServer.Tests' LoggingConfigurationTests show Off sends nothing to Loki
        using var factory = new WebApplicationFactory<Program>();

        // Act
        IConfiguration configuration = factory.Services.GetRequiredService<IConfiguration>();

        // Assert
        Assert.That(configuration["Loki:MinLevel"], Is.EqualTo("Off"));
    }
}
