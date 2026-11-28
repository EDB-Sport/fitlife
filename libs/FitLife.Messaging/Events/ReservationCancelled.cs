using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FitLife.Messaging.Events;

/// <summary>
/// Booking afmeldt: a member's reservation is cancelled. Published by Booking, consumed by Notification. Schema: <c>contracts/events/reservation-cancelled.schema.json</c>, generated from this record (#491, #231).
/// </summary>
/// <param name="ReservationId">The reservation's id in Booking, the same id reservation-made carried.</param>
/// <param name="ClassSessionId">The id of the class session the seat was on.</param>
/// <param name="MemberId">The member whose reservation is cancelled (MemberId, from Membership).</param>
/// <param name="StartTime">When the class session starts, ISO 8601 with an offset.</param>
/// <param name="IsLateCancellation">Sen afmelding: true when the member cancelled under 2 hours before the start.</param>
[Description("Booking afmeldt: a member's reservation on a class session is cancelled, by the member or because the membership is paused or cancelled. Published by Booking, consumed by Notification (docs/domain/CONTEXT-MAP.md section 5). The message carries the correlation ID in its X-Correlation-ID header, not in the body.")]
public sealed record ReservationCancelled(
    [property: Description("The reservation's id in Booking, the same id reservation-made carried. A reservation is cancelled once, so consumers use it to handle the same event only once."), MinLength(1)] string ReservationId,
    [property: Description("The id of the class session the seat was on."), MinLength(1)] string ClassSessionId,
    [property: Description("The member whose reservation is cancelled (MemberId, from Membership)."), MinLength(1)] string MemberId,
    [property: Description("When the class session starts, ISO 8601 with an offset.")] DateTimeOffset StartTime,
    [property: Description("Sen afmelding: true when the member cancelled under 2 hours before the start.")] bool IsLateCancellation) : IIntegrationEvent
{
    /// <inheritdoc />
    public static string RoutingKey => "reservation-cancelled";

    /// <inheritdoc />
    public static IIntegrationEvent Example => new ReservationCancelled(
        "6741f0c2a9e1b3d4c5f60718",
        "6741f0c2a9e1b3d4c5f60719",
        "6741f0c2a9e1b3d4c5f6071a",
        new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero),
        true);
}
