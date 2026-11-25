using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityService.IntegrationTests;

/// <summary>The service logs its name and version when it starts, so every release can be found in Loki.</summary>
public sealed class StartupLogTests
{
    [Test]
    public void Start_LogsServiceNameAndVersion()
    {
        // Arrange
        using var logs = new CapturingLoggerProvider();
        using var app = new WebApplicationFactory<Program>();
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));

        // Act
        using HttpClient client = factory.CreateClient();

        // Assert
        Assert.That(logs.Messages, Has.Some.Contains("IdentityService").And.Contains("0.1.0"));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider, ILogger
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IEnumerable<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Enqueue(formatter(state, exception));
        }

        public void Dispose()
        {
            // Nothing to release: the messages live as long as the test.
        }
    }
}
