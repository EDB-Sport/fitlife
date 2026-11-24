namespace BookingService.Domain;

/// <summary>Kapacitet: how many members can have a reservation on one class session (GLOSSARY.md).</summary>
public readonly record struct Capacity
{
    /// <summary>Creates a capacity of at least one seat.</summary>
    /// <param name="seats">The number of seats, 1 or more.</param>
    public Capacity(int seats)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(seats, 1);
        Seats = seats;
    }

    /// <summary>The number of seats.</summary>
    public int Seats { get; }
}
