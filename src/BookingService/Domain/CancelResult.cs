namespace BookingService.Domain;

/// <summary>What <see cref="ClassSession.Cancel"/> did (#538). Every result but the first changes nothing and raises nothing.</summary>
public enum CancelResult
{
    /// <summary>The class session is cancelled: no reservations, no waitlist entries.</summary>
    Cancelled,

    /// <summary>The class session was cancelled before.</summary>
    AlreadyCancelled,

    /// <summary>The class session has started, so it can't be cancelled any more.</summary>
    ClassSessionStarted,
}
