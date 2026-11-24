namespace BookingService.Domain;

/// <summary>
/// Hold: one scheduled run of a class (GLOSSARY.md). The aggregate root of Booking: reservations change only through it,
/// so the number of reservations never exceeds the capacity (src/BookingService/CONTEXT.md).
/// </summary>
public sealed class ClassSession
{
    private readonly List<Reservation> _reservations = [];

    /// <summary>Creates a class session with no reservations.</summary>
    /// <param name="id">The class session's id.</param>
    /// <param name="capacity">How many members can have a reservation.</param>
    public ClassSession(Guid id, Capacity capacity)
    {
        Id = id;
        Capacity = capacity;
    }

    /// <summary>The class session's id.</summary>
    public Guid Id { get; }

    /// <summary>How many members can have a reservation.</summary>
    public Capacity Capacity { get; }

    /// <summary>The members' seats, in the order they were reserved.</summary>
    public IReadOnlyList<Reservation> Reservations => _reservations;

    /// <summary>Reserves a seat for the member, unless every seat is taken.</summary>
    /// <param name="memberId">The member who wants a seat.</param>
    /// <returns><see cref="ReservationResult.ClassSessionFull"/> when no seat is free; then nothing changes.</returns>
    public ReservationResult Reserve(Guid memberId)
    {
        if (_reservations.Count >= Capacity.Seats)
        {
            return ReservationResult.ClassSessionFull;
        }

        _reservations.Add(new Reservation(memberId));
        return ReservationResult.Made;
    }
}
