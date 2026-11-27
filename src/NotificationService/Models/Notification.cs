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

    /// <summary>The confirmation of a cancelled reservation; the same text for a late one, which is statistics only (Sen afmelding).</summary>
    /// <param name="reservationCancelled">The event from Booking.</param>
    /// <param name="occurredAt">When it is sent.</param>
    /// <returns>The notification to the member whose reservation is cancelled.</returns>
    public static Notification For(ReservationCancelled reservationCancelled, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(reservationCancelled);
        return new Notification(
            $"{ReservationCancelled.RoutingKey}:{reservationCancelled.ReservationId}",
            reservationCancelled.MemberId,
            nameof(ReservationCancelled),
            $"Din booking på holdet den {StartText(reservationCancelled.StartTime)} er afmeldt.",
            occurredAt);
    }

    /// <summary>The notice of a cancelled class session, to everyone who was reserved or waitlisted.</summary>
    /// <param name="classSessionCancelled">The event from Booking.</param>
    /// <param name="occurredAt">When they are sent.</param>
    /// <returns>One notification per member the event lists; none when it lists none.</returns>
    public static IEnumerable<Notification> For(ClassSessionCancelled classSessionCancelled, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(classSessionCancelled);
        string text = $"Dit hold den {StartText(classSessionCancelled.StartTime)} er aflyst. Din booking eller ventelisteplads er fjernet.";
        return classSessionCancelled.MemberIds.Select(memberId => new Notification(
            $"{ClassSessionCancelled.RoutingKey}:{classSessionCancelled.ClassSessionId}:{memberId}",
            memberId,
            nameof(ClassSessionCancelled),
            text,
            occurredAt));
    }

    /// <summary>The notice of a class session's new start, to everyone reserved on it. They keep their reservation.</summary>
    /// <param name="classSessionChanged">The event from Booking.</param>
    /// <param name="occurredAt">When they are sent.</param>
    /// <returns>One notification per member the event lists; none when it lists none.</returns>
    public static IEnumerable<Notification> For(ClassSessionChanged classSessionChanged, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(classSessionChanged);
        // The schema's key is the class session and the new start; as UTC, so one instant gives one id
        string change = $"{ClassSessionChanged.RoutingKey}:{classSessionChanged.ClassSessionId}:{classSessionChanged.StartTime.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}";
        string text = $"Dit hold er flyttet til den {StartText(classSessionChanged.StartTime)}. Din booking gælder stadig, og du kan frit afmelde den.";
        return classSessionChanged.MemberIds.Select(memberId => new Notification(
            $"{change}:{memberId}",
            memberId,
            nameof(ClassSessionChanged),
            text,
            occurredAt));
    }

    // In the offset the event carries, not Danish local time (#545)
    private static string StartText(DateTimeOffset startTime)
    {
        return startTime.ToString("dd.MM.yyyy 'kl.' HH:mm", CultureInfo.InvariantCulture);
    }
}
