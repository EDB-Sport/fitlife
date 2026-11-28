namespace MembershipService.Domain;

/// <summary>The outcome of <see cref="Membership.Cancel"/> (UC8, #249). A rejection changes nothing.</summary>
public enum CancelResult
{
    /// <summary>The membership is Opsagt: the last training day is set.</summary>
    Cancelled = 0,

    /// <summary>The membership is already Opsagt or Afsluttet today.</summary>
    MembershipAlreadyCancelled = 1,
}
