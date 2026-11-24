namespace BookingService.Domain;

/// <summary>What <see cref="ClassSession.CloseWaitlist"/> did.</summary>
public enum WaitlistClosingResult
{
    /// <summary>The remaining waitlist entries were removed, each with a <see cref="WaitlistEntryExpired"/> event.</summary>
    Closed,

    /// <summary>The class session starts more than 2 hours from now, so the waitlist stays open.</summary>
    TooEarly,
}
