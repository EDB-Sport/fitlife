namespace BookingService.Application;

/// <summary>What <see cref="CancelClassSessionCommandHandler"/> did (#239). Every outcome but the first saves nothing.</summary>
public enum CancelClassSessionOutcome
{
    /// <summary>The class session is cancelled and saved: no reservations, no waitlist entries.</summary>
    Cancelled,

    /// <summary>There is no such class session.</summary>
    ClassSessionNotFound,

    /// <summary>The class session was cancelled before.</summary>
    ClassSessionAlreadyCancelled,

    /// <summary>The class session has started, so it can't be cancelled (#538).</summary>
    ClassSessionStarted,
}
