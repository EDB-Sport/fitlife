namespace MembershipService.Domain;

/// <summary>
/// Medlemskabets tilstand: where a membership is in its life (TO-BE-kontekst §8). Only <see cref="Active"/>
/// gives access and the right to book. The other states (Opsagt, Afsluttet) come with their issues.
/// </summary>
public enum MembershipStatus
{
    /// <summary>Aktiv.</summary>
    Active = 0,

    /// <summary>Betaling mangler: the membership fee was not paid (TO-BE-kontekst §8, UC18).</summary>
    PaymentMissing = 1,

    /// <summary>
    /// Pause: inside the membership's pause period (TO-BE-kontekst §8, UC6). Never stored: the status on a day is computed
    /// from the pause period (<see cref="Membership.StatusOn"/>).
    /// </summary>
    Paused = 2,
}
