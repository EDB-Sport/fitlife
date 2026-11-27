using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FitLife.Messaging.Events;

/// <summary>
/// Medlemskab pauset: a member paused the membership. Published by Membership, consumed by Booking. Schema: <c>contracts/events/membership-paused.schema.json</c>, generated from this record (#491, #231).
/// </summary>
/// <param name="MemberId">The member whose membership is paused (MemberId).</param>
/// <param name="From">The first day of the pause, a date without a time.</param>
/// <param name="To">The last day of the pause, a date without a time.</param>
[Description("Medlemskab pauset: a member paused the membership for a period, both days included. Published by Membership, consumed by Booking, which frees the member's seats inside the period (docs/domain/CONTEXT-MAP.md section 5). The message carries the correlation ID in its X-Correlation-ID header, not in the body.")]
public sealed record MembershipPaused(
    [property: Description("The member whose membership is paused (MemberId). Consumers use it with from to handle the same event only once."), MinLength(1)] string MemberId,
    [property: Description("The first day of the pause, a date without a time.")] DateOnly From,
    [property: Description("The last day of the pause, a date without a time.")] DateOnly To) : IIntegrationEvent
{
    /// <inheritdoc />
    public static string RoutingKey => "membership-paused";

    /// <inheritdoc />
    public static IIntegrationEvent Example => new MembershipPaused(
        "6741f0c2a9e1b3d4c5f6071a",
        new DateOnly(2026, 12, 1),
        new DateOnly(2026, 12, 31));
}
