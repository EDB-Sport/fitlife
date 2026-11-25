namespace BookingService.Api;

/// <summary>A member's place on the waitlist of a class session.</summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="MemberId">The member who is waiting.</param>
public sealed record WaitlistEntryResponse(Guid ClassSessionId, Guid MemberId);
