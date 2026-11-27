using FitLife.Messaging.Events;
using MembershipService.Domain;

namespace MembershipService.Application;

/// <summary>
/// Joins an account to FitLife: one member per account, and a membership that becomes active once the first fee is paid.
/// A fee that isn't paid leaves the membership with Betaling mangler and publishes <see cref="PaymentFailed"/> (#251).
/// </summary>
/// <param name="members">The members.</param>
/// <param name="memberships">The memberships.</param>
/// <param name="fee">The monthly fee and the payment provider that charges it.</param>
/// <param name="clock">Today's date, and when a charge failed.</param>
/// <param name="events">Publishes the integration event once the membership is stored.</param>
public sealed class JoinMembershipHandler(
    IMemberRepository members,
    IMembershipRepository memberships,
    MembershipFee fee,
    TimeProvider clock,
    MembershipEventPublisher events)
{
    /// <summary>Joins the account, unless it is already a member.</summary>
    /// <param name="command">The account that joins.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new member and membership, or that the account was already a member.</returns>
    public async Task<JoinResult> HandleAsync(JoinMembershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await members.FindByAccountIdAsync(command.AccountId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return JoinResult.AlreadyAMember();
        }

        var member = Member.Join(command.AccountId);
        var membership = Membership.Start(member.Id, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));

        // The membership first: a lookup goes through the member, so a membership without one is never seen
        await memberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);
        if (!await members.TryAddAsync(member, cancellationToken).ConfigureAwait(false))
        {
            // Another join for the same account got there first
            await memberships.RemoveAsync(membership.Id, cancellationToken).ConfigureAwait(false);
            return JoinResult.AlreadyAMember();
        }

        // Charged once the membership is stored, so a paid fee always has a membership. It is stored with Betaling mangler and
        // made active only after the charge is approved: a join stopped before or during the charge leaves no free access.
        // Once the charge starts, a cancelled request stops neither it nor the save of its result (#544): the provider's own
        // time-out still applies
        cancellationToken.ThrowIfCancellationRequested();
        bool paid = await fee.ChargeAsync(member.Id, CancellationToken.None).ConfigureAwait(false);
        if (paid)
        {
            membership.MarkPaid();
            await memberships.SaveAsync(membership, CancellationToken.None).ConfigureAwait(false);
        }
        else
        {
            // The membership is stored with Betaling mangler already (above)
            await events.PublishAsync(new PaymentFailed(member.Id.ToString(), clock.GetUtcNow())).ConfigureAwait(false);
        }

        return JoinResult.Success(member, membership);
    }
}
