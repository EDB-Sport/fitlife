using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FitLife.Messaging.Events;

/// <summary>
/// Medlemskab opsagt: a member cancelled the membership with notice. Published by Membership, consumed by Booking. Schema: <c>contracts/events/membership-cancelled.schema.json</c>, generated from this record (#491, #231).
/// </summary>
/// <param name="MemberId">The member whose membership is cancelled (MemberId).</param>
/// <param name="LastTrainingDay">The last day the member may train, a date without a time.</param>
[Description("Medlemskab opsagt: a member cancelled the membership with notice. Published by Membership, consumed by Booking, which frees the member's seats after the last training day (docs/domain/CONTEXT-MAP.md section 5). The message carries the correlation ID in its X-Correlation-ID header, not in the body.")]
public sealed record MembershipCancelled(
    [property: Description("The member whose membership is cancelled (MemberId). A membership is cancelled once, so consumers use it to handle the same event only once."), MinLength(1)] string MemberId,
    [property: Description("The last day the member may train, a date without a time.")] DateOnly LastTrainingDay) : IIntegrationEvent
{
    /// <inheritdoc />
    public static string RoutingKey => "membership-cancelled";

    /// <inheritdoc />
    public static IIntegrationEvent Example => new MembershipCancelled(
        "6741f0c2a9e1b3d4c5f6071a",
        new DateOnly(2027, 1, 31));
}
