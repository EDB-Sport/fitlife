namespace BookingService.Domain;

/// <summary>Instruktør: the trainer who leads a class session (GLOSSARY.md). Booking knows the instructor by name only.</summary>
public sealed record Instructor
{
    /// <summary>Creates an instructor with a name.</summary>
    /// <param name="name">The instructor's name; not empty or white space.</param>
    public Instructor(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The instructor's name.</summary>
    public string Name { get; }
}
