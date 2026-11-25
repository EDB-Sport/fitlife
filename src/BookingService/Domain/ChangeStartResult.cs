namespace BookingService.Domain;

/// <summary>What <see cref="ClassSession.ChangeStart"/> did.</summary>
public enum ChangeStartResult
{
    /// <summary>The class session has the new start; its reservations and waitlist entries are kept.</summary>
    Changed,

    /// <summary>The class session is cancelled, so its start can't change.</summary>
    ClassSessionCancelled,

    /// <summary>The new start is now or in the past.</summary>
    StartNotInTheFuture,
}
