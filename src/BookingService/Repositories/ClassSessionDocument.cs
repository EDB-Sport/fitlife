using MongoDB.Bson.Serialization.Attributes;

namespace BookingService.Repositories;

/// <summary>How a class session is stored in MongoDB. Only <see cref="ClassSessionRepository"/> uses it.</summary>
internal sealed class ClassSessionDocument
{
    [BsonId]
    public Guid Id { get; init; }

    public Guid CenterId { get; init; }

    public Guid ClassTypeId { get; init; }

    public string InstructorName { get; init; } = "";

    public TimeSpan Duration { get; init; }

    public int Capacity { get; init; }

    /// <summary>In UTC. MongoDB stores a date to the millisecond.</summary>
    public DateTime StartTime { get; init; }

    public bool IsCancelled { get; init; }

    public List<ReservationDocument> Reservations { get; init; } = [];

    public List<Guid> Waitlist { get; init; } = [];

    /// <summary>Goes up by one on every save, so a save on a stale copy finds no document (optimistic concurrency).</summary>
    public int Version { get; init; }
}
