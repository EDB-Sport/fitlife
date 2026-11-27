namespace BookingService.Application;

/// <summary>What <see cref="CancelReservationCommandHandler"/> answered (#262).</summary>
public enum CancelReservationOutcome
{
    /// <summary>The reservation is cancelled and saved; the class session gave the seat to the first waitlist entry, if any.</summary>
    Cancelled,

    /// <summary>The member has no reservation on the class session, or the account is not a member. Nothing was saved.</summary>
    NoReservation,

    /// <summary>There is no such class session.</summary>
    ClassSessionNotFound,

    /// <summary>Membership gave no usable answer, so Booking can't tell who the member is.</summary>
    MembershipUnavailable,

    /// <summary>Another request saved the class session first. Nothing was saved.</summary>
    ChangedMeanwhile,
}
