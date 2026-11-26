namespace MembershipService.Domain;

/// <summary>
/// Medlemskabets tilstand: where a membership is in its life (TO-BE-kontekst §8). Only <see cref="Active"/>
/// gives access and the right to book. The other states (Pause, Opsagt, Afsluttet) come with their issues.
/// </summary>
public enum MembershipStatus
{
    /// <summary>Aktiv.</summary>
    Active = 0,

    /// <summary>Betaling mangler: the membership fee was not paid (TO-BE-kontekst §8, UC18).</summary>
    PaymentMissing = 1,
}
