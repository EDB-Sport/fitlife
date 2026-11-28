namespace BookingService.Domain;

/// <summary>Booking: a member's seat on one class session (GLOSSARY.md). Created only by <see cref="ClassSession"/>.</summary>
public sealed class Reservation
{
    /// <summary>A new seat, with a new id.</summary>
    internal Reservation(Guid memberId, bool isFromWaitlist)
        : this(Guid.NewGuid(), memberId, isFromWaitlist, isFromBeforeAChangedStart: false)
    {
    }

    /// <summary>A seat as it was stored.</summary>
    internal Reservation(Guid id, Guid memberId, bool isFromWaitlist, bool isFromBeforeAChangedStart)
    {
        Id = id;
        MemberId = memberId;
        IsFromWaitlist = isFromWaitlist;
        IsFromBeforeAChangedStart = isFromBeforeAChangedStart;
    }

    /// <summary>The reservation's own id, which <c>ReservationMade</c> carries to the other contexts (#192).</summary>
    public Guid Id { get; }

    /// <summary>The member who has the seat. Booking refers to a member only by id.</summary>
    public Guid MemberId { get; }

    /// <summary>The seat came from the waitlist, so cancelling it is never a late cancellation.</summary>
    public bool IsFromWaitlist { get; }

    /// <summary>The seat was reserved before the class session's start changed, so cancelling it is never a late cancellation.</summary>
    public bool IsFromBeforeAChangedStart { get; }

    /// <summary>The same seat, marked as reserved before a changed start.</summary>
    internal Reservation FromBeforeAChangedStart()
    {
        return new Reservation(Id, MemberId, IsFromWaitlist, isFromBeforeAChangedStart: true);
    }
}
