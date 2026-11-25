namespace BookingService.Domain;

/// <summary>Hold aflyst: a class session will not take place (GLOSSARY.md).</summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="MemberIds">The members who had a reservation or a waitlist entry, so Notification can tell each of them.</param>
public sealed record ClassSessionCancelled(Guid ClassSessionId, IReadOnlyList<Guid> MemberIds) : DomainEvent(ClassSessionId);
