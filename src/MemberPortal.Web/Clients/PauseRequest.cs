namespace MemberPortal.Web.Clients;

/// <summary>The body of Membership's <c>PUT members/me/pause</c>: the first and the last day of the pause, both included (#211).</summary>
/// <param name="From">The first day of the pause.</param>
/// <param name="To">The last day of the pause.</param>
public sealed record PauseRequest(DateOnly From, DateOnly To);
