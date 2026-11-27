namespace BookingService.Application;

/// <summary>What <see cref="ChangeClassSessionStartCommandHandler"/> did (#241). Every outcome but the first saves nothing.</summary>
public enum ChangeClassSessionStartOutcome
{
    /// <summary>The class session has the new start and is saved; its reservations and waitlist entries are kept.</summary>
    Changed,

    /// <summary>There is no such class session.</summary>
    ClassSessionNotFound,

    /// <summary>The class session is cancelled, so its start can't change.</summary>
    ClassSessionCancelled,

    /// <summary>The new start is now or in the past.</summary>
    StartNotInTheFuture,

    /// <summary>The class session has started, so its start can't change (#538).</summary>
    ClassSessionStarted,

    /// <summary>Another request saved the class session first.</summary>
    ChangedMeanwhile,
}
