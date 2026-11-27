namespace MemberPortal.Web.Clients;

/// <summary>Membership's answer to pausing and ending a pause: its <c>MembershipPauseResponse</c> (contracts/openapi/membershipservice.json).</summary>
/// <param name="MemberId">The member id.</param>
/// <param name="MembershipType">Medlemstype, by name, e.g. <c>Standard</c>.</param>
/// <param name="MembershipStatus">Medlemskabets tilstand today, by name, e.g. <c>Paused</c>.</param>
/// <param name="IsActive">Whether the member may book and enter today.</param>
/// <param name="StartDate">The day the membership started.</param>
/// <param name="PausedFrom">The first day of the pause period, or <see langword="null"/>.</param>
/// <param name="PausedTo">The last day of the pause period, or <see langword="null"/>.</param>
public sealed record MembershipPauseResponse(
    Guid MemberId, string MembershipType, string MembershipStatus, bool IsActive, DateOnly StartDate, DateOnly? PausedFrom, DateOnly? PausedTo);
