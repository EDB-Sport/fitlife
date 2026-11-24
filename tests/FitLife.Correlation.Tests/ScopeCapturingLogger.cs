using Microsoft.Extensions.Logging;

namespace FitLife.Correlation.Tests;

/// <summary>A logger that records the scopes begun on it, so tests can see what reaches NLog's ${scopeproperty}.</summary>
internal sealed class ScopeCapturingLogger<T> : ILogger<T>
{
    public List<object> Scopes { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        Scopes.Add(state);
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Only scopes are recorded.
    }
}
