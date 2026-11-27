using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FitLife.Messaging.Events;

/// <summary>
/// Hold ændret: a class session got a new start time. Published by Booking, consumed by Notification.
/// Schema: <c>contracts/events/class-session-changed.schema.json</c>, generated from this record (#231).
/// </summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="MemberIds">The members who had a reservation at the change, not the waitlisted. May be empty.</param>
/// <param name="PreviousStartTime">When it was going to start.</param>
/// <param name="StartTime">When it starts now.</param>
[Description("Hold ændret: a class session got a new start time. Published by Booking, consumed by Notification (docs/domain/CONTEXT-MAP.md section 5). The message carries the correlation ID in its X-Correlation-ID header, not in the body.")]
public sealed record ClassSessionChanged(
    [property: Description("The id of the class session. Consumers use it with startTime to handle the same event only once."), MinLength(1)] string ClassSessionId,
    [property: Description("The members who had a reservation at the change, not the waitlisted (MemberId, from Membership). May be empty.")] IReadOnlyList<string> MemberIds,
    [property: Description("When the class session was going to start, ISO 8601 with an offset.")] DateTimeOffset PreviousStartTime,
    [property: Description("When the class session starts now, ISO 8601 with an offset.")] DateTimeOffset StartTime) : IIntegrationEvent
{
    /// <inheritdoc />
    public static string RoutingKey => "class-session-changed";

    /// <inheritdoc />
    public static IIntegrationEvent Example => new ClassSessionChanged(
        "6741f0c2a9e1b3d4c5f60719",
        ["6741f0c2a9e1b3d4c5f6071a"],
        new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 12, 1, 18, 0, 0, TimeSpan.Zero));
}
