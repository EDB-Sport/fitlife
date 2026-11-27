namespace MembershipService.Domain;

/// <summary>The outcome of <see cref="Membership.Pause"/>, checked in this order (UC6, #248). A rejection changes nothing.</summary>
public enum PauseResult
{
    /// <summary>The pause period is kept. It replaces a pause period still to come.</summary>
    Paused = 0,

    /// <summary>The membership isn't Aktiv today: Betaling mangler, or already inside a pause period.</summary>
    MembershipNotActive = 1,

    /// <summary>The pause starts before today. Today itself is fine.</summary>
    PauseStartsInThePast = 2,

    /// <summary>The pause doesn't end after it starts: one day or less is rejected.</summary>
    PauseEndsBeforeItStarts = 3,
}
