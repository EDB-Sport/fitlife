namespace BookingService.Application;

/// <summary>A member gives up their seat on a class session (#262).</summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="AccountId">The account id from the member's token, never from the route or the body.</param>
/// <param name="BearerToken">The member's token, passed on to Membership unchanged.</param>
public sealed record CancelReservationCommand(Guid ClassSessionId, Guid AccountId, string BearerToken);
