namespace MembershipService.Application;

/// <summary>A person with an account joins FitLife (UC5).</summary>
/// <param name="AccountId">The account id from the token.</param>
public sealed record JoinMembershipCommand(string AccountId);
