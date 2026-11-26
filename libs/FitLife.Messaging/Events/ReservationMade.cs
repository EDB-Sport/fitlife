namespace FitLife.Messaging.Events;

/// <summary>
/// Booking oprettet: a member got a seat on a class session, directly or from the waitlist.
/// Published by Booking, consumed by Notification. Schema: <c>contracts/events/reservation-made.schema.json</c>.
/// </summary>
/// <param name="ReservationId">The reservation's id in Booking.</param>
/// <param name="ClassSessionId">The class session the seat is on.</param>
/// <param name="MemberId">The member who got the seat.</param>
/// <param name="StartTime">When the class session starts.</param>
public sealed record ReservationMade(string ReservationId, string ClassSessionId, string MemberId, DateTimeOffset StartTime) : IIntegrationEvent
{
    /// <inheritdoc />
    public static string RoutingKey => "reservation-made";
}
