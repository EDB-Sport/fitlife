namespace BookingService.Domain;

/// <summary>What <see cref="ClassSession.CancelReservation"/> did.</summary>
public enum CancellationResult
{
    /// <summary>The member's reservation was cancelled.</summary>
    Cancelled,

    /// <summary>The member has no reservation on the class session, so nothing changed.</summary>
    NoReservation,
}
