using MembershipService.Domain;
using Microsoft.Extensions.Options;

namespace MembershipService.Application;

/// <summary>
/// Charges the missing monthly fee of a membership with Betaling mangler and, when the charge is accepted, makes it Aktiv (UC18).
/// A membership that owes nothing is never charged.
/// </summary>
/// <param name="members">The members.</param>
/// <param name="memberships">The memberships.</param>
/// <param name="payments">The payment provider, behind its anticorruption layer.</param>
/// <param name="fee">The monthly fee.</param>
public sealed class PayMembershipHandler(
    IMemberRepository members,
    IMembershipRepository memberships,
    IPaymentProvider payments,
    IOptions<MembershipFeeSettings> fee)
{
    /// <summary>Charges the missing fee of the account's membership.</summary>
    /// <param name="command">The account that pays.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What happened.</returns>
    public async Task<PayMembershipResult> HandleAsync(PayMembershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Member? member = await members.FindByAccountIdAsync(command.AccountId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return PayMembershipResult.NotAMember;
        }

        Membership? membership = await memberships.FindByMemberIdAsync(member.Id, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return PayMembershipResult.NotAMember;
        }

        // Checked before the charge, so a member who owes nothing is never charged. The rule itself is RegisterPayment's
        if (membership.Status != MembershipStatus.PaymentMissing)
        {
            return PayMembershipResult.NothingIsOwed;
        }

        if (!await payments.ChargeMembershipFeeAsync(member.Id, fee.Value.MonthlyFeeDkk, cancellationToken).ConfigureAwait(false))
        {
            return PayMembershipResult.ChargeDeclined;
        }

        membership.RegisterPayment();
        await memberships.SaveAsync(membership, cancellationToken).ConfigureAwait(false);
        return PayMembershipResult.Paid;
    }
}
