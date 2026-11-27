namespace MemberPortal.Web.Clients;

/// <summary>Membership's answer to joining: its <c>MemberResponse</c> (contracts/openapi/membershipservice.json).</summary>
/// <param name="MemberId">The member id other contexts store.</param>
/// <param name="MembershipStatus">Medlemskabets tilstand, by name: <c>Active</c>, <c>PaymentMissing</c> or <c>Paused</c>.</param>
/// <param name="IsActive">Whether the member may book and enter.</param>
public sealed record MemberResponse(Guid MemberId, string MembershipStatus, bool IsActive);
