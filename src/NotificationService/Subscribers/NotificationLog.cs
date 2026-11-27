using FitLife.Correlation;
using NotificationService.Models;
using NotificationService.Repositories;

namespace NotificationService.Subscribers;

/// <summary>
/// Stores a notification and then sends it, which in the PoC is one log line in #191's format. A notification that was
/// stored before (the event came again) is not sent again.
/// </summary>
internal static partial class NotificationLog
{
    /// <summary>Stores and sends each notification that wasn't stored before.</summary>
    /// <param name="notifications">The notifications sent so far.</param>
    /// <param name="toSend">The notifications one event gives.</param>
    /// <param name="logger">The subscriber's logger.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when every notification is handled.</returns>
    public static async Task SendAsync(INotificationRepository notifications, IEnumerable<Notification> toSend, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (Notification notification in toSend)
        {
            if (!await notifications.TryAddAsync(notification, cancellationToken).ConfigureAwait(false))
            {
                LogAlreadySent(logger, notification.Id);
                continue;
            }

            LogNotification(logger, notification.MemberId, notification.Text, CorrelationContext.Current);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification to member {MemberId}: {Text} (correlation ID {CorrelationId})")]
    private static partial void LogNotification(ILogger logger, string memberId, string text, string? correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification {NotificationId} was already sent; the event came again")]
    private static partial void LogAlreadySent(ILogger logger, string notificationId);
}
