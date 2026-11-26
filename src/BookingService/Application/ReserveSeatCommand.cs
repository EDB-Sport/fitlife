namespace BookingService.Application;

/// <summary>A member asks for a seat on a class session (#260).</summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="AccountId">The account id from the member's token, never from the request body.</param>
/// <param name="BearerToken">The member's token, passed on to Membership unchanged.</param>
public sealed record ReserveSeatCommand(Guid ClassSessionId, Guid AccountId, string BearerToken);
