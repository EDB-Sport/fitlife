using MembershipService.Domain;
using Microsoft.Extensions.Options;

namespace MembershipService.Application;

/// <summary>The monthly fee and the payment provider that charges it.</summary>
/// <param name="payments">The payment provider, behind its anticorruption layer.</param>
/// <param name="settings">The monthly fee.</param>
public sealed class MembershipFee(IPaymentProvider payments, IOptions<MembershipFeeSettings> settings)
{
    /// <summary>Charges the monthly fee.</summary>
    /// <param name="memberId">The member who pays.</param>
    /// <param name="cancellationToken">Cancels the charge.</param>
    /// <returns>True when the provider accepted the charge.</returns>
    public Task<bool> ChargeAsync(Guid memberId, CancellationToken cancellationToken)
    {
        return payments.ChargeMembershipFeeAsync(memberId, settings.Value.MonthlyFeeDkk, cancellationToken);
    }
}
