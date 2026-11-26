namespace MembershipService.Domain;

/// <summary>The outcome of <see cref="Membership.EndPause"/> (UC6, #248). A rejection changes nothing.</summary>
public enum EndPauseResult
{
    /// <summary>A pause period that wasn't over, running or still to come, is removed: the membership is Aktiv from today.</summary>
    PauseEnded = 0,

    /// <summary>There is no pause period, or it is over.</summary>
    NoPauseToEnd = 1,
}
