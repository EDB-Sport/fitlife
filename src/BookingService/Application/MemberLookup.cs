namespace BookingService.Application;

/// <summary>What Membership said about an account: found, not a member, or unavailable.</summary>
/// <param name="Outcome">Which of the three it is.</param>
/// <param name="MemberId">The member id when found, otherwise <see cref="Guid.Empty"/>.</param>
/// <param name="IsActive">Whether the membership gives the right to book; only "is active" crosses the boundary.</param>
public sealed record MemberLookup(MemberLookupOutcome Outcome, Guid MemberId, bool IsActive)
{
    /// <summary>Gets the answer for an account that is not a member (Membership answered 404).</summary>
    public static MemberLookup NotAMember { get; } = new(MemberLookupOutcome.NotAMember, Guid.Empty, IsActive: false);

    /// <summary>Gets the answer when Membership gave none we can use: 5xx after the retries, a time-out, an open circuit, no connection.</summary>
    public static MemberLookup Unavailable { get; } = new(MemberLookupOutcome.Unavailable, Guid.Empty, IsActive: false);

    /// <summary>The account is a member.</summary>
    /// <param name="memberId">The member id.</param>
    /// <param name="isActive">Whether the membership is active.</param>
    /// <returns>The answer.</returns>
    public static MemberLookup Found(Guid memberId, bool isActive)
    {
        return new MemberLookup(MemberLookupOutcome.Found, memberId, isActive);
    }
}
