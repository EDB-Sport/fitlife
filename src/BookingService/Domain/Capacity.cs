namespace BookingService.Domain;

/// <summary>Kapacitet: how many members can have a reservation on one class session (GLOSSARY.md).</summary>
public readonly record struct Capacity
{
    /// <summary>Creates a capacity of at least one.</summary>
    /// <param name="value">How many members can have a reservation, 1 or more.</param>
    public Capacity(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        Value = value;
    }

    /// <summary>How many members can have a reservation.</summary>
    public int Value { get; }
}
