using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FitLife.Messaging.Events;

/// <summary>
/// Hold aflyst: a class session will not take place. Published by Booking, consumed by Notification.
/// Schema: <c>contracts/events/class-session-cancelled.schema.json</c>, generated from this record (#231).
/// </summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="MemberIds">The members who had a reservation or a waitlist entry. May be empty.</param>
/// <param name="StartTime">When the class session was going to start.</param>
[Description("Hold aflyst: a class session will not take place. Published by Booking, consumed by Notification (docs/domain/CONTEXT-MAP.md section 5). The message carries the correlation ID in its X-Correlation-ID header, not in the body.")]
public sealed record ClassSessionCancelled(
    [property: Description("The id of the class session. A class session is cancelled once, so consumers use it, with the member id per notice, to handle the same event only once."), MinLength(1)] string ClassSessionId,
    [property: Description("The members who had a reservation or a waitlist entry (MemberId, from Membership). May be empty.")] IReadOnlyList<string> MemberIds,
    [property: Description("When the class session was going to start, ISO 8601 with an offset.")] DateTimeOffset StartTime) : IIntegrationEvent
{
    /// <inheritdoc />
    public static string RoutingKey => "class-session-cancelled";

    /// <inheritdoc />
    public static IIntegrationEvent Example => new ClassSessionCancelled(
        "6741f0c2a9e1b3d4c5f60719",
        ["6741f0c2a9e1b3d4c5f6071a", "6741f0c2a9e1b3d4c5f6071b"],
        new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero));
}
