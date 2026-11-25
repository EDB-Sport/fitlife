using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MembershipService.IntegrationTests;

/// <summary>MembershipService runs the correlation ID middleware, so the log lines of a request carry its X-Correlation-ID (#19).</summary>
public sealed class CorrelationIdTests
{
    [Test]
    public async Task Request_WithCorrelationIdHeader_LogsWithThatIdInTheScope()
    {
        // Arrange
        using var logs = new ScopeCapturingLoggerProvider();
        using var app = new MembershipFactory();
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host
            .UseSetting("Logging:LogLevel:Microsoft.AspNetCore", "Information")
            .ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Add("X-Correlation-ID", "membership-test-correlation-id");

        // Act
        using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

        // Assert
        Assert.That(logs.CorrelationIds, Has.Some.EqualTo("membership-test-correlation-id"));
    }

    // Records the CorrelationId of the scope each log line is written in
    private sealed class ScopeCapturingLoggerProvider : ILoggerProvider, ILogger, ISupportExternalScope
    {
        private readonly ConcurrentQueue<string> _correlationIds = new();
        private IExternalScopeProvider? _scopes;

        public IEnumerable<string> CorrelationIds => _correlationIds;

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopes = scopeProvider;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return _scopes?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _scopes?.ForEachScope(
                (scope, ids) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> values)
                    {
                        foreach (KeyValuePair<string, object?> value in values)
                        {
                            if (string.Equals(value.Key, "CorrelationId", StringComparison.Ordinal) && value.Value is string id)
                            {
                                ids.Enqueue(id);
                            }
                        }
                    }
                },
                _correlationIds);
        }

        public void Dispose()
        {
            // Nothing to release: the IDs live as long as the test.
        }
    }
}
