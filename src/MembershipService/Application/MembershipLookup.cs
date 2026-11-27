using MembershipService.Domain;

namespace MembershipService.Application;

/// <summary>Finds an account's membership through its member, and gives today as the UTC date.</summary>
internal static class MembershipLookup
{
    /// <summary>The membership of the account, or null when the account hasn't joined.</summary>
    /// <param name="members">The members.</param>
    /// <param name="memberships">The memberships.</param>
    /// <param name="accountId">The account id from the token.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The membership, or null.</returns>
    public static async Task<Membership?> FindByAccountIdAsync(
        IMemberRepository members, IMembershipRepository memberships, string accountId, CancellationToken cancellationToken)
    {
        Member? member = await members.FindByAccountIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return null;
        }

        return await memberships.FindByMemberIdAsync(member.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Today as the UTC date.</summary>
    /// <param name="clock">The clock.</param>
    /// <returns>Today.</returns>
    public static DateOnly Today(TimeProvider clock)
    {
        return DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    }
}
