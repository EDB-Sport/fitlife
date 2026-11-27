using System.Globalization;
using FitLife.Messaging.Events;

namespace NotificationService.Models;

/// <summary>
/// Notifikation: one message to one member about something that happened. In the PoC it is only logged
/// (CONTEXT-MAP section 4), and stored so the same event never gives two notifications.
/// </summary>
/// <param name="Id">The event it is about, e.g. <c>reservation-made:&lt;reservation id&gt;</c>: one notification per event.</param>
/// <param name="MemberId">The member it is for.</param>
/// <param name="Kind">The event it is about, by name, e.g. <c>ReservationMade</c>.</param>
/// <param name="Text">What the member is told.</param>
/// <param name="OccurredAt">When it was sent.</param>
public sealed record Notification(string Id, string MemberId, string Kind, string Text, DateTimeOffset OccurredAt)
{
    /// <summary>The confirmation of a reservation.</summary>
    /// <param name="reservationMade">The event from Booking.</param>
    /// <param name="occurredAt">When it is sent.</param>
    /// <returns>The notification to the member who got the seat.</returns>
    public static Notification For(ReservationMade reservationMade, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(reservationMade);
        // In the offset the event carries, not Danish local time (#545)
        string startTime = reservationMade.StartTime.ToString("dd.MM.yyyy 'kl.' HH:mm", CultureInfo.InvariantCulture);
        return new Notification(
            $"{ReservationMade.RoutingKey}:{reservationMade.ReservationId}",
            reservationMade.MemberId,
            nameof(ReservationMade),
            $"Din booking på holdet den {startTime} er bekræftet.",
            occurredAt);
    }
}
