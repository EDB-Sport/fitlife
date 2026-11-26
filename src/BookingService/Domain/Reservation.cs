namespace BookingService.Domain;

/// <summary>Booking: a member's seat on one class session (GLOSSARY.md). Created only by <see cref="ClassSession"/>.</summary>
public sealed class Reservation
{
    internal Reservation(Guid memberId, bool isFromWaitlist)
    {
        MemberId = memberId;
        IsFromWaitlist = isFromWaitlist;
    }

    private Reservation(Guid memberId, bool isFromWaitlist, bool isFromBeforeAChangedStart)
        : this(memberId, isFromWaitlist)
    {
        IsFromBeforeAChangedStart = isFromBeforeAChangedStart;
    }

    /// <summary>The member who has the seat. Booking refers to a member only by id.</summary>
    public Guid MemberId { get; }

    /// <summary>The seat came from the waitlist, so cancelling it is never a late cancellation.</summary>
    public bool IsFromWaitlist { get; }

    /// <summary>The seat was reserved before the class session's start changed, so cancelling it is never a late cancellation.</summary>
    public bool IsFromBeforeAChangedStart { get; }

    /// <summary>The same seat, marked as reserved before a changed start.</summary>
    internal Reservation FromBeforeAChangedStart()
    {
        return new Reservation(MemberId, IsFromWaitlist, isFromBeforeAChangedStart: true);
    }
}
