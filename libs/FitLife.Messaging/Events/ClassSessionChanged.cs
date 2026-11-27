using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FitLife.Messaging.Events;

/// <summary>
/// Hold ændret: a class session got a new start. Published by Booking, consumed by Notification. Schema: <c>contracts/events/class-session-changed.schema.json</c>, generated from this record (#491, #231).
/// </summary>
/// <param name="ClassSessionId">The id of the class session.</param>
/// <param name="MemberIds">The members who have a reservation on it (MemberId, from Membership); not the waitlisted.</param>
/// <param name="PreviousStartTime">When the class session started before the change, ISO 8601 with an offset.</param>
/// <param name="StartTime">When the class session starts from now on, ISO 8601 with an offset.</param>
[Description("Hold ændret: a class session got a new start. Its reservations are kept. Published by Booking, consumed by Notification (docs/domain/CONTEXT-MAP.md section 5). The message carries the correlation ID in its X-Correlation-ID header, not in the body.")]
public sealed record ClassSessionChanged(
    [property: Description("The id of the class session. Consumers use it with startTime to handle the same event only once."), MinLength(1)] string ClassSessionId,
    [property: Description("The members who have a reservation on it (MemberId, from Membership); not the waitlisted. May be empty."), MinLength(1)] IReadOnlyList<string> MemberIds,
    [property: Description("When the class session started before the change, ISO 8601 with an offset.")] DateTimeOffset PreviousStartTime,
    [property: Description("When the class session starts from now on, ISO 8601 with an offset.")] DateTimeOffset StartTime) : IIntegrationEvent
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
