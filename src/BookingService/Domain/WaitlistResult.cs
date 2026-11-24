namespace BookingService.Domain;

/// <summary>What <see cref="ClassSession.JoinWaitlist"/> did.</summary>
public enum WaitlistResult
{
    /// <summary>The member got a waitlist entry at the end of the waitlist.</summary>
    Joined,

    /// <summary>A seat is free, so the member can reserve it instead; no waitlist entry was made.</summary>
    ClassSessionNotFull,
}
