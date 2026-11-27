using MembershipService.Domain;

namespace MembershipService.Application;

/// <summary>Cancels the membership of the account in the token (UC8). Saves only when the root cancels it.</summary>
/// <param name="members">The members.</param>
/// <param name="memberships">The memberships.</param>
/// <param name="clock">Today's date, in UTC.</param>
public sealed class CancelMembershipHandler(IMemberRepository members, IMembershipRepository memberships, TimeProvider clock)
{
    /// <summary>Cancels the membership, unless it is already cancelled.</summary>
    /// <param name="command">The account.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The root's answer, or that the account hasn't joined.</returns>
    public async Task<MembershipCommandResult<CancelResult>> HandleAsync(CancelMembershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Membership? membership = await MembershipLookup.FindByAccountIdAsync(members, memberships, command.AccountId, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return MembershipCommandResult.NotAMember<CancelResult>();
        }

        CancelResult result = membership.Cancel(MembershipLookup.Today(clock));
        if (result == CancelResult.Cancelled)
        {
            await memberships.SaveAsync(membership, cancellationToken).ConfigureAwait(false);
        }

        return MembershipCommandResult.Answered(membership, result);
    }
}
