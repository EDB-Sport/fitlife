namespace NotificationService.Api;

/// <summary>One notification in a member's delivery log.</summary>
/// <param name="Id">The notification, one per event.</param>
/// <param name="MemberId">The member it was sent to.</param>
/// <param name="Kind">The event it is about, by name, e.g. <c>ReservationMade</c>.</param>
/// <param name="Text">What the member was told.</param>
/// <param name="OccurredAt">When it was sent.</param>
public sealed record NotificationResponse(string Id, string MemberId, string Kind, string Text, DateTimeOffset OccurredAt);
