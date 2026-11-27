using FitLife.Messaging;
using FitLife.Messaging.Events;
using NotificationService.Models;
using NotificationService.Repositories;

namespace NotificationService.Subscribers;

/// <summary>
/// Confirms a cancelled reservation to the member, with the same text for a late cancellation (CONTEXT-MAP section 5).
/// The PoC only logs the notification; real SMS, e-mail or push sit behind an ACL later.
/// </summary>
/// <param name="notifications">The notifications sent so far.</param>
/// <param name="time">The clock the notification's time comes from.</param>
/// <param name="logger">The logger the notification is written to.</param>
public sealed class ReservationCancelledSubscriber(INotificationRepository notifications, TimeProvider time, ILogger<ReservationCancelledSubscriber> logger)
    : IEventSubscriber<ReservationCancelled>
{
    /// <inheritdoc />
    public Task HandleAsync(ReservationCancelled integrationEvent, CancellationToken cancellationToken)
    {
        return NotificationLog.SendAsync(notifications, [Notification.For(integrationEvent, time.GetUtcNow())], logger, cancellationToken);
    }
}
