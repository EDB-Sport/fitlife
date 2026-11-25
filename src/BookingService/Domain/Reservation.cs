namespace BookingService.Domain;

/// <summary>Booking: a member's seat on one class session (GLOSSARY.md). Created only by <see cref="ClassSession"/>.</summary>
public sealed class Reservation
{
    internal Reservation(Guid memberId)
    {
        MemberId = memberId;
    }

    /// <summary>The member who has the seat. Booking refers to a member only by id.</summary>
    public Guid MemberId { get; }
}
