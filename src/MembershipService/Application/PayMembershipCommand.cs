namespace MembershipService.Application;

/// <summary>A member with Betaling mangler pays the missing membership fee (UC18).</summary>
/// <param name="AccountId">The account id from the token.</param>
public sealed record PayMembershipCommand(string AccountId);
