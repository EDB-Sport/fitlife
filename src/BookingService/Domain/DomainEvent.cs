namespace BookingService.Domain;

/// <summary>Something that happened in a <see cref="ClassSession"/>. Events are named in the past tense (METHODOLOGY §11.2 rule 6).</summary>
/// <param name="ClassSessionId">The class session it happened in.</param>
public abstract record DomainEvent(Guid ClassSessionId);
