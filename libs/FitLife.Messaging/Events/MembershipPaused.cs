using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FitLife.Messaging.Events;

/// <summary>
/// Medlemskab pauset: a membership is on pause for a period. Published by Membership, consumed by Notification.
/// Schema: <c>contracts/events/membership-paused.schema.json</c>, generated from this record (#231).
/// </summary>
/// <param name="MemberId">The member whose membership is paused.</param>
/// <param name="From">The first day of the pause period.</param>
/// <param name="To">The last day of the pause period.</param>
[Description("Medlemskab pauset: a membership is on pause for a period. Published by Membership, consumed by Notification (docs/domain/CONTEXT-MAP.md section 5). The message carries the correlation ID in its X-Correlation-ID header, not in the body.")]
public sealed record MembershipPaused(
    [property: Description("The member whose membership is paused (MemberId). Consumers use it with from to handle the same event only once."), MinLength(1)] string MemberId,
    [property: Description("The first day of the pause period, a date without a time.")] DateOnly From,
    [property: Description("The last day of the pause period, included, a date without a time.")] DateOnly To) : IIntegrationEvent
{
    /// <inheritdoc />
    public static string RoutingKey => "membership-paused";

    /// <inheritdoc />
    public static IIntegrationEvent Example => new MembershipPaused("6741f0c2a9e1b3d4c5f6071a", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31));
}
