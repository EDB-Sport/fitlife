namespace MembershipService.Domain;

/// <summary>
/// Betalingsudbyder: the external payment provider that charges the membership fee, in our terms.
/// The implementation is the anticorruption layer (CONTEXT-MAP section 4), so the provider's names stay outside the model.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Charges the member's membership fee.</summary>
    /// <param name="memberId">The member who pays.</param>
    /// <param name="amountDkk">The fee in Danish kroner.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the fee is paid. False when it was declined or the provider could not tell.</returns>
    Task<bool> ChargeMembershipFeeAsync(Guid memberId, decimal amountDkk, CancellationToken cancellationToken);
}
