namespace MemberPortal.Web.Clients;

/// <summary>Membership's answer to paying the missing fee (#535).</summary>
/// <param name="Membership">The membership after a 200, or <see langword="null"/> when Membership said no.</param>
/// <param name="RejectionCode">The problem details' <c>code</c> of a 409, e.g. <c>ChargeDeclined</c>; <see langword="null"/> on a 200 and on an answer without a code.</param>
public sealed record PaymentAnswer(MyMembershipResponse? Membership, string? RejectionCode);
