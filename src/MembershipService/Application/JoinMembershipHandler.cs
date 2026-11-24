using MembershipService.Domain;
using Microsoft.Extensions.Options;

namespace MembershipService.Application;

/// <summary>
/// Joins an account to FitLife: one member per account, with an active membership, and the first fee charged.
/// A fee that isn't paid gives the membership Betaling mangler.
/// </summary>
/// <param name="members">The members.</param>
/// <param name="memberships">The memberships.</param>
/// <param name="payments">The payment provider, behind its anticorruption layer.</param>
/// <param name="fee">The monthly fee.</param>
/// <param name="clock">Today's date.</param>
public sealed class JoinMembershipHandler(
    IMemberRepository members,
    IMembershipRepository memberships,
    IPaymentProvider payments,
    IOptions<MembershipFeeSettings> fee,
    TimeProvider clock)
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

        // Charged once the membership is stored, so a paid fee always has a membership
        bool paid = await payments.ChargeMembershipFeeAsync(member.Id, fee.Value.MonthlyFeeDkk, cancellationToken).ConfigureAwait(false);
        if (!paid)
        {
            membership.MarkPaymentMissing();
            await memberships.SaveAsync(membership, cancellationToken).ConfigureAwait(false);
        }

        return JoinResult.Success(member, membership);
    }
}
