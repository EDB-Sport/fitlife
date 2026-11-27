namespace MembershipService.Application;

/// <summary>A member ends the pause of their membership early (UC6).</summary>
/// <param name="AccountId">The account id from the token.</param>
public sealed record EndPauseCommand(string AccountId);
