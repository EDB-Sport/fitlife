namespace BookingService.Domain;

/// <summary>Booking afmeldt: a member gave up their seat (GLOSSARY.md).</summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="MemberId">The member who gave up the seat.</param>
/// <param name="IsLateCancellation">
/// Sen afmelding: cancelled less than 2 hours before start. Statistics only, no consequence. A seat from the waitlist is never late.
/// </param>
public sealed record ReservationCancelled(Guid ClassSessionId, Guid MemberId, bool IsLateCancellation) : DomainEvent(ClassSessionId);
