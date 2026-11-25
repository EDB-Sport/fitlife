using MembershipService.Domain;

namespace MembershipService.Api;

/// <summary>A member and whether the membership gives access and the right to book.</summary>
/// <param name="MemberId">The member id other contexts store.</param>
/// <param name="MembershipStatus">Where the membership is in its life.</param>
/// <param name="IsActive">Whether the member may book and enter.</param>
public sealed record MemberResponse(Guid MemberId, MembershipStatus MembershipStatus, bool IsActive);
