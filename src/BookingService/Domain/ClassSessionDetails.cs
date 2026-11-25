namespace BookingService.Domain;

/// <summary>
/// Holdoplysninger: where, what and with whom a class session is, and how long it lasts (GLOSSARY.md). Its start time is
/// <see cref="ClassSession.StartTime"/>.
/// </summary>
public sealed record ClassSessionDetails
{
    /// <summary>Creates the details of a class session.</summary>
    /// <param name="centerId">The centre it takes place in. Booking refers to a centre by id.</param>
    /// <param name="classTypeId">The kind of class it is a run of. Booking refers to a class type by id.</param>
    /// <param name="instructor">The trainer who leads it.</param>
    /// <param name="duration">How long it lasts; more than zero.</param>
    public ClassSessionDetails(Guid centerId, Guid classTypeId, Instructor instructor, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(instructor);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        CenterId = centerId;
        ClassTypeId = classTypeId;
        Instructor = instructor;
        Duration = duration;
    }

    /// <summary>The centre the class session takes place in.</summary>
    public Guid CenterId { get; }

    /// <summary>The kind of class (Holdtype) it is a run of.</summary>
    public Guid ClassTypeId { get; }

    /// <summary>The trainer who leads it.</summary>
    public Instructor Instructor { get; }

    /// <summary>How long it lasts.</summary>
    public TimeSpan Duration { get; }
}
