namespace MembershipService.Application;

/// <summary>A member cancels their membership with one month's notice (UC8). One call, no body.</summary>
/// <param name="AccountId">The account id from the token.</param>
public sealed record CancelMembershipCommand(string AccountId);
