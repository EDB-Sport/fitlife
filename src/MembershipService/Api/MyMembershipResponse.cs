using MembershipService.Domain;

namespace MembershipService.Api;

/// <summary>The signed-in member's own membership, for the page "Mit medlemskab" (#210).</summary>
/// <param name="MemberId">The member id.</param>
/// <param name="MembershipType">The kind of membership.</param>
/// <param name="MembershipStatus">Where the membership is in its life.</param>
/// <param name="IsActive">Whether the member may book and enter.</param>
/// <param name="StartDate">The day the membership started.</param>
/// <param name="LastTrainingDay">The last day the membership gives access after the member cancelled it, or null (UC8).</param>
public sealed record MyMembershipResponse(
    Guid MemberId, MembershipType MembershipType, MembershipStatus MembershipStatus, bool IsActive, DateOnly StartDate, DateOnly? LastTrainingDay);
