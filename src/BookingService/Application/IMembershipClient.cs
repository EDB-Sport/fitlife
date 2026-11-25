namespace BookingService.Application;

/// <summary>
/// Membership's answer to "who is the member behind this account, and is the membership active" (CONTEXT-MAP section 4,
/// Open Host Service over REST). Never throws for a missing member or an unavailable Membership.
/// </summary>
public interface IMembershipClient
{
    /// <summary>Asks Membership for the member behind the account, passing the member's own token on unchanged (#173).</summary>
    /// <param name="accountId">The account id from the token (<c>sub</c>).</param>
    /// <param name="bearerToken">The member's token, without the <c>Bearer</c> prefix.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Found, not a member, or unavailable.</returns>
    Task<MemberLookup> FindMemberAsync(Guid accountId, string bearerToken, CancellationToken cancellationToken);
}
