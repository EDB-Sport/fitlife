using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FitLife.Messaging.Events;

/// <summary>
/// Booking oprettet: a member got a seat on a class session, directly or from the waitlist.
/// Published by Booking, consumed by Notification. Schema: <c>contracts/events/reservation-made.schema.json</c>,
/// generated from this record (#491): the descriptions and <c>MinLength</c> below go into it.
/// </summary>
/// <param name="ReservationId">The reservation's id in Booking.</param>
/// <param name="ClassSessionId">The class session the seat is on.</param>
/// <param name="MemberId">The member who got the seat.</param>
/// <param name="StartTime">When the class session starts.</param>
[Description("Booking oprettet: a member got a seat on a class session, directly or from the waitlist. Published by Booking, consumed by Notification (docs/domain/CONTEXT-MAP.md section 5). The message carries the correlation ID in its X-Correlation-ID header, not in the body.")]
public sealed record ReservationMade(
    [property: Description("The reservation's id in Booking. Consumers use it to handle the same event only once."), MinLength(1)] string ReservationId,
    [property: Description("The id of the class session the seat is on."), MinLength(1)] string ClassSessionId,
    [property: Description("The member who got the seat (MemberId, from Membership)."), MinLength(1)] string MemberId,
    [property: Description("When the class session starts, ISO 8601 with an offset.")] DateTimeOffset StartTime) : IIntegrationEvent
{
    /// <inheritdoc />
    public static string RoutingKey => "reservation-made";

    /// <inheritdoc />
    public static IIntegrationEvent Example => new ReservationMade(
        "6741f0c2a9e1b3d4c5f60718",
        "6741f0c2a9e1b3d4c5f60719",
        "6741f0c2a9e1b3d4c5f6071a",
        new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero));
}
