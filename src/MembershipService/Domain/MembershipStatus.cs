namespace MembershipService.Domain;

/// <summary>
/// Medlemskabets tilstand: where a membership is in its life (TO-BE-kontekst §8). Only <see cref="Active"/>
/// gives access and the right to book.
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

    /// <summary>
    /// Opsagt: the member has given notice (TO-BE-kontekst §8, UC8). Stored when the member cancels; on a day after the last
    /// training day the status is <see cref="Ended"/>.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// Afsluttet: the last training day is over (TO-BE-kontekst §8, UC8). Never stored: computed from
    /// <see cref="Membership.LastTrainingDay"/> (<see cref="Membership.StatusOn"/>).
    /// </summary>
    Ended = 4,
}
