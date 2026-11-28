namespace MemberPortal.Web.Clients;

/// <summary>The signed-in member's own membership: Membership's <c>MyMembershipResponse</c> (contracts/openapi/membershipservice.json).</summary>
/// <param name="MemberId">The member id.</param>
/// <param name="MembershipType">Medlemstype, by name, e.g. <c>Standard</c>.</param>
/// <param name="MembershipStatus">Medlemskabets tilstand, by name, e.g. <c>Active</c>.</param>
/// <param name="IsActive">Whether the member may book and enter.</param>
/// <param name="StartDate">The day the membership started.</param>
/// <param name="LastTrainingDay">The last day the membership gives access after the member cancelled it, or null.</param>
public sealed record MyMembershipResponse(Guid MemberId, string MembershipType, string MembershipStatus, bool IsActive, DateOnly StartDate, DateOnly? LastTrainingDay = null);
