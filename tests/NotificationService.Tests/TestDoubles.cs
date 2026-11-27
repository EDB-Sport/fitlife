using Microsoft.Extensions.Logging;
using NotificationService.Models;
using NotificationService.Repositories;

namespace NotificationService.Tests;

/// <summary>The repository's contract: a second notification with the same id is not added.</summary>
internal sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly Dictionary<string, Notification> _stored = new(StringComparer.Ordinal);

    public IReadOnlyCollection<Notification> Stored => _stored.Values;

    public Task<bool> TryAddAsync(Notification notification, CancellationToken cancellationToken)
    {
        return Task.FromResult(_stored.TryAdd(notification.Id, notification));
    }

    public Task<IReadOnlyList<Notification>> ListForMemberAsync(string memberId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Notification> listed = [.. _stored.Values.Where(notification => string.Equals(notification.MemberId, memberId, StringComparison.Ordinal)).OrderByDescending(notification => notification.OccurredAt)];
        return Task.FromResult(listed);
    }
}

/// <summary>The clock, stopped at one instant.</summary>
internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return now;
    }
}

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
