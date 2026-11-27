namespace MembershipService.Domain;

/// <summary>What <see cref="Membership.RegisterPayment"/> did with a paid fee.</summary>
public enum PaymentResult
{
    /// <summary>The fee was missing: the membership is Aktiv again.</summary>
    Paid = 0,

    /// <summary>The membership doesn't have Betaling mangler, so nothing is owed and nothing changed.</summary>
    NothingIsOwed = 1,
}
