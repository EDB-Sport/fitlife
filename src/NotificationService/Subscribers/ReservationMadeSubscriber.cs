using FitLife.Correlation;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using NotificationService.Models;
using NotificationService.Repositories;

namespace NotificationService.Subscribers;

/// <summary>
/// Confirms a reservation to the member, also when the seat came from the waitlist (CONTEXT-MAP section 5).
/// The PoC only logs the notification; real SMS, e-mail or push sit behind an ACL later.
/// </summary>
/// <param name="notifications">The notifications sent so far.</param>
/// <param name="logger">The logger the notification is written to.</param>
public sealed partial class ReservationMadeSubscriber(INotificationRepository notifications, ILogger<ReservationMadeSubscriber> logger)
    : IEventSubscriber<ReservationMade>
{
    /// <inheritdoc />
    public async Task HandleAsync(ReservationMade integrationEvent, CancellationToken cancellationToken)
    {
        var notification = Notification.For(integrationEvent);
        if (!await notifications.TryAddAsync(notification, cancellationToken).ConfigureAwait(false))
        {
            LogAlreadySent(notification.Id);
            return;
        }

        LogNotification(notification.MemberId, notification.Text, CorrelationContext.Current);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification to member {MemberId}: {Text} (correlation ID {CorrelationId})")]
    private partial void LogNotification(string memberId, string text, string? correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification {NotificationId} was already sent; the event came again")]
    private partial void LogAlreadySent(string notificationId);
}
