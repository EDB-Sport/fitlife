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
