namespace MemberPortal.Web.Clients;

/// <summary>Membership's answer to pausing or ending a pause (#211).</summary>
/// <param name="Membership">The membership after a 200, or <see langword="null"/> when Membership said no.</param>
/// <param name="RejectionCode">The problem details' <c>code</c> of a 409, e.g. <c>PauseStartsInThePast</c>; <see langword="null"/> on a 200 and on an answer without a code.</param>
public sealed record PauseAnswer(MembershipPauseResponse? Membership, string? RejectionCode);
