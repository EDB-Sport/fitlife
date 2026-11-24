namespace BookingService.Domain;

/// <summary>What <see cref="ClassSession.JoinWaitlist"/> did.</summary>
public enum WaitlistResult
{
    /// <summary>The member got a waitlist entry at the end of the waitlist.</summary>
    Joined,

    /// <summary>A seat is free, so the member can reserve it instead; no waitlist entry was made.</summary>
    ClassSessionNotFull,

    /// <summary>The class session starts in less than 2 hours, so the waitlist is closed; no waitlist entry was made.</summary>
    WaitlistClosed,
}
