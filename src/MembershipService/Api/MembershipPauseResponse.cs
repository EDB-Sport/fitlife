using MembershipService.Domain;

namespace MembershipService.Api;

/// <summary>The signed-in member's membership after a pause was set or ended: the body of <c>GET /members/me</c> plus the pause period.</summary>
/// <param name="MemberId">The member id.</param>
/// <param name="MembershipType">The kind of membership.</param>
/// <param name="MembershipStatus">Where the membership is today.</param>
/// <param name="IsActive">Whether the member may book and enter today.</param>
/// <param name="StartDate">The day the membership started.</param>
/// <param name="PausedFrom">The first day of the pause period, or null.</param>
/// <param name="PausedTo">The last day of the pause period, or null.</param>
public sealed record MembershipPauseResponse(
    Guid MemberId, MembershipType MembershipType, MembershipStatus MembershipStatus, bool IsActive, DateOnly StartDate, DateOnly? PausedFrom, DateOnly? PausedTo);
