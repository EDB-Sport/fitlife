namespace BookingService.Domain;

/// <summary>
/// Ventelisteplads udløbet: the waitlist closed 2 hours before start and the member's waitlist entry was removed without a
/// seat (GLOSSARY.md).
/// </summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="MemberId">The member who didn't get a seat.</param>
public sealed record WaitlistEntryExpired(Guid ClassSessionId, Guid MemberId) : DomainEvent(ClassSessionId);
