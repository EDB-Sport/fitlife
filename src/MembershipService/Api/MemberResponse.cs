using MembershipService.Domain;

namespace MembershipService.Api;

/// <summary>A member and whether the membership gives access and the right to book.</summary>
/// <param name="MemberId">The member id other contexts store.</param>
/// <param name="MembershipStatus">Where the membership is in its life.</param>
/// <param name="IsActive">Whether the member may book and enter.</param>
/// <param name="PausedFrom">The first day of a pause period that is running or still to come, or null.</param>
/// <param name="PausedTo">The last day of that pause period, or null.</param>
public sealed record MemberResponse(Guid MemberId, MembershipStatus MembershipStatus, bool IsActive, DateOnly? PausedFrom, DateOnly? PausedTo);
