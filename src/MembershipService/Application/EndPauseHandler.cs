using MembershipService.Domain;

namespace MembershipService.Application;

/// <summary>Ends the pause of the membership of the account in the token early (UC6). Saves only when a pause was ended.</summary>
/// <param name="members">The members.</param>
/// <param name="memberships">The memberships.</param>
/// <param name="clock">Today's date, in UTC.</param>
public sealed class EndPauseHandler(IMemberRepository members, IMembershipRepository memberships, TimeProvider clock)
{
    /// <summary>Ends the pause, unless there is none to end.</summary>
    /// <param name="command">The account.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The root's answer, or that the account hasn't joined.</returns>
    public async Task<MembershipCommandResult<EndPauseResult>> HandleAsync(EndPauseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Membership? membership = await MembershipLookup.FindByAccountIdAsync(members, memberships, command.AccountId, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return MembershipCommandResult.NotAMember<EndPauseResult>();
        }

        EndPauseResult result = membership.EndPause(MembershipLookup.Today(clock));
        if (result == EndPauseResult.PauseEnded)
        {
            await memberships.SaveAsync(membership, cancellationToken).ConfigureAwait(false);
        }

        return MembershipCommandResult.Answered(membership, result);
    }
}
