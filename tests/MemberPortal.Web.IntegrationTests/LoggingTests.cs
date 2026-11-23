using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MemberPortal.Web.IntegrationTests;

/// <summary>The member portal logs like the services: a startup line with its version, and the request's correlation ID.</summary>
public sealed class LoggingTests
{
    private static WebApplicationFactory<Program> WithCapturedLogs(WebApplicationFactory<Program> app, CapturingLoggerProvider logs)
    {
        return app.WithWebHostBuilder(host => host
            .ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs))
            .ConfigureLogging(logging => logging.AddFilter<CapturingLoggerProvider>(null, LogLevel.Trace)));
    }

    [Test]
    public void Start_LogsNameAndVersion()
    {
        // Arrange
        using var logs = new CapturingLoggerProvider();
        using var app = new WebApplicationFactory<Program>();
        using var factory = WithCapturedLogs(app, logs);

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.That(logs.Messages, Has.Some.Contains("MemberPortal.Web").And.Contains("0.1.0"));
    }

    [Test]
    public async Task Request_WithCorrelationId_LogsItInTheScope()
    {
        // Arrange
        using var logs = new CapturingLoggerProvider();
        using var app = new WebApplicationFactory<Program>();
        using var factory = WithCapturedLogs(app, logs);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Add("X-Correlation-ID", "abc-123");

        // Act
        using var response = await client.SendAsync(request).ConfigureAwait(false);

        // Assert
        var scopeValues = logs.Scopes.SelectMany(scopes => scopes).OfType<IEnumerable<KeyValuePair<string, object?>>>().SelectMany(pairs => pairs);
        Assert.That(scopeValues, Has.Member(new KeyValuePair<string, object?>("CorrelationId", "abc-123")));
    }
}
