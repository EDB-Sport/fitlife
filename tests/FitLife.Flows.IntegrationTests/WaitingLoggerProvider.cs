using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FitLife.Flows.IntegrationTests;

/// <summary>Keeps a host's log lines, so a test can wait for one instead of sleeping (TEAM007).</summary>
internal sealed class WaitingLoggerProvider : ILoggerProvider, ILogger
{
    private readonly ConcurrentQueue<string> _lines = new();
    private readonly SemaphoreSlim _logged = new(0);

    /// <summary>Waits for the first line that contains the text.</summary>
    /// <param name="text">What the line contains.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <returns>The line.</returns>
    public async Task<string> WaitForAsync(string text, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        while (true)
        {
            string? line = _lines.FirstOrDefault(candidate => candidate.Contains(text, StringComparison.Ordinal));
            if (line is not null)
            {
                return line;
            }

            await _logged.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return this;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _lines.Enqueue(formatter(state, exception));
        _logged.Release();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _logged.Dispose();
    }
}
