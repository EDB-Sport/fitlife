namespace BookingService.Domain;

/// <summary>Booking oprettet: a member got a seat on a class session, directly or from the waitlist (GLOSSARY.md).</summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="MemberId">The member who got the seat.</param>
public sealed record ReservationMade(Guid ClassSessionId, Guid MemberId) : DomainEvent(ClassSessionId);
