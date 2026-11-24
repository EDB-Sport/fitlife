using Microsoft.Extensions.Logging;

namespace FitLife.Correlation;

/// <summary>
/// The correlation ID of the request or message being handled, so one booking can be followed across every service in Grafana.
/// </summary>
public static class CorrelationContext
{
    /// <summary>The HTTP header and RabbitMQ message header that carries the ID. Nginx sets it when a request has none.</summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>The logging scope property, read by <c>${scopeproperty:CorrelationId}</c> in libs/nlog.config.</summary>
    public const string ScopeKey = "CorrelationId";

    private static readonly AsyncLocal<string?> CurrentId = new();

    /// <summary>Gets the ID of the request or message being handled, or null outside one.</summary>
    public static string? Current => CurrentId.Value;

    /// <summary>Makes <paramref name="id"/> the current ID and puts it in the logging scope until the result is disposed.</summary>
    /// <param name="logger">Any logger: the scope applies to every logger in the service.</param>
    /// <param name="id">The correlation ID.</param>
    /// <returns>Dispose it when the request or message has been handled.</returns>
    public static IDisposable Begin(ILogger logger, string id)
    {
        ArgumentNullException.ThrowIfNull(logger);
        string? previous = CurrentId.Value;
        CurrentId.Value = id;
        IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>(StringComparer.Ordinal) { [ScopeKey] = id });
        return new Restore(previous, scope);
    }

    private sealed class Restore(string? previous, IDisposable? scope) : IDisposable
    {
        public void Dispose()
        {
            scope?.Dispose();
            CurrentId.Value = previous;
        }
    }
}
