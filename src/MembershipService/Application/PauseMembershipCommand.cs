namespace MembershipService.Application;

/// <summary>A member pauses their membership from <paramref name="From"/> to <paramref name="To"/>, both included (UC6).</summary>
/// <param name="AccountId">The account id from the token.</param>
/// <param name="From">The first day of the pause.</param>
/// <param name="To">The last day of the pause.</param>
public sealed record PauseMembershipCommand(string AccountId, DateOnly From, DateOnly To);
