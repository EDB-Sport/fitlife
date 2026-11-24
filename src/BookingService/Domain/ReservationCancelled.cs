namespace BookingService.Domain;

/// <summary>Booking afmeldt: a member gave up their seat (GLOSSARY.md).</summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="MemberId">The member who gave up the seat.</param>
public sealed record ReservationCancelled(Guid ClassSessionId, Guid MemberId) : DomainEvent(ClassSessionId);
