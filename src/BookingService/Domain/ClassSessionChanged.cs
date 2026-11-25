namespace BookingService.Domain;

/// <summary>Hold ændret: a class session got a new start time (GLOSSARY.md).</summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="MemberIds">The members who had a reservation at the change (not the waitlisted), so Notification can tell each of them.</param>
/// <param name="PreviousStartTime">When it was going to start.</param>
/// <param name="StartTime">When it starts now.</param>
public sealed record ClassSessionChanged(Guid ClassSessionId, IReadOnlyList<Guid> MemberIds, DateTimeOffset PreviousStartTime, DateTimeOffset StartTime)
    : DomainEvent(ClassSessionId);
