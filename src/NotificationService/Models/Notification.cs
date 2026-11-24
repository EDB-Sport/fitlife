using System.Globalization;
using FitLife.Messaging.Events;

namespace NotificationService.Models;

/// <summary>
/// Notifikation: one message to one member about something that happened. In the PoC it is only logged
/// (CONTEXT-MAP section 4), and stored so the same event never gives two notifications.
/// </summary>
/// <param name="Id">The event it is about, e.g. <c>reservation-made:&lt;reservation id&gt;</c>: one notification per event.</param>
/// <param name="MemberId">The member it is for.</param>
/// <param name="Text">What the member is told.</param>
public sealed record Notification(string Id, string MemberId, string Text)
{
    /// <summary>The confirmation of a reservation.</summary>
    /// <param name="reservationMade">The event from Booking.</param>
    /// <returns>The notification to the member who got the seat.</returns>
    public static Notification For(ReservationMade reservationMade)
    {
        ArgumentNullException.ThrowIfNull(reservationMade);
        string startTime = reservationMade.StartTime.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture);
        return new Notification(
            $"{ReservationMade.RoutingKey}:{reservationMade.ReservationId}",
            reservationMade.MemberId,
            $"Your seat on class session {reservationMade.ClassSessionId} at {startTime} is reserved.");
    }
}
