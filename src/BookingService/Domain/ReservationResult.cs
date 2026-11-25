namespace BookingService.Domain;

/// <summary>What <see cref="ClassSession.Reserve"/> did.</summary>
public enum ReservationResult
{
    /// <summary>The member got a seat (a reservation was made).</summary>
    Made,

    /// <summary>Every seat is taken, so no reservation was made.</summary>
    ClassSessionFull,

    /// <summary>The member already has a seat on this class session, so no second reservation was made.</summary>
    MemberAlreadyHasReservation,
}
