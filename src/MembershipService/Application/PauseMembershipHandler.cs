using FitLife.Messaging.Events;
using MembershipService.Domain;

namespace MembershipService.Application;

/// <summary>
/// Pauses the membership of the account in the token (UC6). Saves only when the root keeps the pause period, and then publishes
/// <see cref="MembershipPaused"/> so Booking can free the member's seats (#251).
/// </summary>
/// <param name="members">The members.</param>
/// <param name="memberships">The memberships.</param>
/// <param name="clock">Today's date, in UTC.</param>
/// <param name="events">Publishes the integration event once the pause is saved.</param>
public sealed class PauseMembershipHandler(
    IMemberRepository members,
    IMembershipRepository memberships,
    TimeProvider clock,
    MembershipEventPublisher events)
{
    /// <summary>Pauses the membership, unless the root rejects it.</summary>
    /// <param name="command">The account and the pause period.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The root's answer, or that the account hasn't joined.</returns>
    public async Task<MembershipCommandResult<PauseResult>> HandleAsync(PauseMembershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Membership? membership = await MembershipLookup.FindByAccountIdAsync(members, memberships, command.AccountId, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return MembershipCommandResult.NotAMember<PauseResult>();
        }

        PauseResult result = membership.Pause(command.From, command.To, MembershipLookup.Today(clock));
        if (result == PauseResult.Paused)
        {
            await memberships.SaveAsync(membership, cancellationToken).ConfigureAwait(false);
            await events.PublishAsync(new MembershipPaused(membership.MemberId.ToString(), command.From, command.To)).ConfigureAwait(false);
        }

        return MembershipCommandResult.Answered(membership, result);
    }
}
