using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MemberPortal.Web.IntegrationTests;

/// <summary>A logger provider for tests: records every message with the logging scopes it was written in.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope, ILogger
{
    private readonly ConcurrentQueue<(string Message, IReadOnlyList<object?> Scopes)> _entries = new();
    private IExternalScopeProvider? _scopes;

    public IEnumerable<string> Messages => _entries.Select(entry => entry.Message);

    public IEnumerable<IReadOnlyList<object?>> Scopes => _entries.Select(entry => entry.Scopes);

    public ILogger CreateLogger(string categoryName)
    {
        return this;
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopes = scopeProvider;
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
        var scopes = new List<object?>();
        _scopes?.ForEachScope((scope, list) => list.Add(scope), scopes);
        _entries.Enqueue((formatter(state, exception), scopes));
    }

    public void Dispose()
    {
        // Nothing to release: the entries live as long as the test.
    }
}
