using System.Collections.Concurrent;
using IdentityService.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityService.IntegrationTests.Accounts;

/// <summary>The app with an in-memory <see cref="IAccountRepository"/>, and every log line of every level captured.</summary>
internal sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The accounts the app has stored.</summary>
    public InMemoryAccountRepository Repository { get; } = new();

    /// <summary>Every log line the app wrote, with its exception.</summary>
    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.AddFilter<CapturingLoggerProvider>(null, LogLevel.Trace));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IAccountRepository>(Repository);
            services.AddSingleton<ILoggerProvider>(Logs);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Logs.Dispose();
        }
    }

    /// <summary>Keeps every log line, formatted, with the exception if there is one.</summary>
    internal sealed class CapturingLoggerProvider : ILoggerProvider, ILogger
    {
        private readonly ConcurrentQueue<string> _lines = new();

        public IEnumerable<string> Lines => _lines;

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
            _lines.Enqueue($"{formatter(state, exception)} {state} {exception}");
        }

        public void Dispose()
        {
            // Nothing to release: the lines live as long as the test.
        }
    }
}
