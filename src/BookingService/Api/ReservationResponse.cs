namespace BookingService.Api;

/// <summary>A member's seat on a class session.</summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="MemberId">The member who has the seat.</param>
public sealed record ReservationResponse(Guid ClassSessionId, Guid MemberId);
