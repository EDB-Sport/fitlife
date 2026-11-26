namespace BookingService.Application;

/// <summary>How far <see cref="ReserveSeatCommandHandler"/> got (#260).</summary>
public enum ReserveSeatOutcome
{
    /// <summary>The class session decided; <see cref="ReserveSeatResult.Reservation"/> says what.</summary>
    Answered,

    /// <summary>The account is not a member, or the membership isn't Aktiv, so it gives no right to book.</summary>
    MembershipNotActive,

    /// <summary>Membership gave no usable answer, so Booking can't tell whether the member may book.</summary>
    MembershipUnavailable,

    /// <summary>There is no such class session.</summary>
    ClassSessionNotFound,

    /// <summary>Another request saved the class session first, also on the second try. Nothing was saved.</summary>
    ConcurrentChange,
}
