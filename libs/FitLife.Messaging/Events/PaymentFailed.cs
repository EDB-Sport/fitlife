using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FitLife.Messaging.Events;

/// <summary>
/// Betaling fejlet: a charge for the membership fee did not go through. Published by Membership, consumed by Notification.
/// Schema: <c>contracts/events/payment-failed.schema.json</c>, generated from this record (#231).
/// </summary>
/// <param name="MemberId">The member whose fee was not paid.</param>
/// <param name="FailedAt">When the charge failed.</param>
[Description("Betaling fejlet: a charge for the membership fee did not go through. Published by Membership, consumed by Notification (docs/domain/CONTEXT-MAP.md section 5). The message carries the correlation ID in its X-Correlation-ID header, not in the body.")]
public sealed record PaymentFailed(
    [property: Description("The member whose fee was not paid (MemberId). Consumers use it with failedAt to handle the same event only once."), MinLength(1)] string MemberId,
    [property: Description("When the charge failed, ISO 8601 with an offset.")] DateTimeOffset FailedAt) : IIntegrationEvent
{
    /// <inheritdoc />
    public static string RoutingKey => "payment-failed";

    /// <inheritdoc />
    public static IIntegrationEvent Example => new PaymentFailed("6741f0c2a9e1b3d4c5f6071a", new DateTimeOffset(2026, 12, 1, 6, 0, 0, TimeSpan.Zero));
}
