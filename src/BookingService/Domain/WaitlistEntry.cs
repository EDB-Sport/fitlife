namespace BookingService.Domain;

/// <summary>Ventelisteplads: a member's place in the queue for a full class session (GLOSSARY.md). Created only by <see cref="ClassSession"/>.</summary>
public sealed class WaitlistEntry
{
    internal WaitlistEntry(Guid memberId)
    {
        MemberId = memberId;
    }

    /// <summary>The member who is waiting. Booking refers to a member only by id.</summary>
    public Guid MemberId { get; }
}
