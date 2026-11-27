namespace MembershipService.Api;

/// <summary>The body of <c>PUT /members/me/pause</c>: <c>{ "from": "2026-12-01", "to": "2026-12-31" }</c>, both days included.</summary>
/// <param name="From">The first day of the pause. Missing is a 400.</param>
/// <param name="To">The last day of the pause. Missing is a 400.</param>
public sealed record PauseRequest(DateOnly? From, DateOnly? To);
