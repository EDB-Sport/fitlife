using System.Collections.Concurrent;
using IdentityService.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace IdentityService.IntegrationTests.Accounts;

/// <summary>The app with an in-memory <see cref="IAccountRepository"/>, and every log line of every level captured.</summary>
internal sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    private readonly IAccountRepository? _replacement;

    /// <summary>The app with an empty <see cref="InMemoryAccountRepository"/>.</summary>
    public IdentityApiFactory()
    {
    }

    /// <summary>The app with <paramref name="replacement"/> as its repository, e.g. one that fails.</summary>
    /// <param name="replacement">The repository the app uses instead of <see cref="Repository"/>.</param>
    public IdentityApiFactory(IAccountRepository replacement)
    {
        _replacement = replacement;
    }

    /// <summary>The accounts the app has stored.</summary>
    public InMemoryAccountRepository Repository { get; } = new();

    /// <summary>The test signing key the app writes its tokens with (Jwt:SigningKey).</summary>
    public const string SigningKey = "test-signing-key-for-identity-tests-only";

    /// <summary>The app's clock.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Every log line the app wrote, with its exception.</summary>
    public CapturingLoggerProvider Logs { get; } = new();

    /// <summary>More settings for the app, e.g. <c>Seed:DemoAccounts</c>. Set them before the first client is created.</summary>
    public Dictionary<string, string> Settings { get; } = new(StringComparer.Ordinal);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        foreach ((string key, string value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureLogging(logging => logging.AddFilter<CapturingLoggerProvider>(null, LogLevel.Trace));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(_replacement ?? Repository);
            services.AddSingleton<ILoggerProvider>(Logs);
            services.AddSingleton<TimeProvider>(Time);
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
