using Microsoft.AspNetCore.Mvc.Testing;
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
}
