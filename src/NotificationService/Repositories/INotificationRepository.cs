using NotificationService.Models;

namespace NotificationService.Repositories;

/// <summary>The notifications that have been sent. The only way to the database (TEAM001).</summary>
public interface INotificationRepository
{
    /// <summary>Stores a notification, unless one with the same id is already stored.</summary>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True if it was stored now; false if it had been stored before (the event came again).</returns>
    Task<bool> TryAddAsync(Notification notification, CancellationToken cancellationToken);

    /// <summary>The delivery log of one member: the notifications sent to them, newest first.</summary>
    /// <param name="memberId">The member.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The member's notifications; empty when there are none.</returns>
    Task<IReadOnlyList<Notification>> ListForMemberAsync(string memberId, CancellationToken cancellationToken);
}
