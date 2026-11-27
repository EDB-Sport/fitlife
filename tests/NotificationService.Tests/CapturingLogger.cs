using Microsoft.Extensions.Logging;

namespace NotificationService.Tests;

/// <summary>Captures every log line of one category.</summary>
/// <typeparam name="TCategory">The class that logs.</typeparam>
internal sealed class CapturingLogger<TCategory> : ILogger<TCategory>
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Gets the lines that tell a member about a notification (#191's format).</summary>
    public IEnumerable<string> NotificationLines => _lines.Where(line => line.StartsWith("Notification to member ", StringComparison.Ordinal));

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
        _lines.Add(formatter(state, exception));
    }
}
