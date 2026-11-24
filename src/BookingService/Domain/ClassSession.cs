namespace BookingService.Domain;

/// <summary>
/// Hold: one scheduled run of a class (GLOSSARY.md). The aggregate root of Booking: reservations change only through it,
/// so the number of reservations never exceeds the capacity, no one reserves more than 4 weeks ahead, a member has at most one, the waitlist keeps its order, and a freed seat goes to the first waitlist entry (src/BookingService/CONTEXT.md).
/// </summary>
public sealed class ClassSession
{
    private readonly List<Reservation> _reservations = [];
    private readonly List<WaitlistEntry> _waitlist = [];
    private readonly List<DomainEvent> _domainEvents = [];

    private static readonly TimeSpan HowFarAheadMembersCanReserve = TimeSpan.FromDays(28);

    /// <summary>Creates a class session with no reservations.</summary>
    /// <param name="id">The class session's id.</param>
    /// <param name="capacity">How many members can have a reservation.</param>
    /// <param name="startTime">When the class session starts.</param>
    public ClassSession(Guid id, Capacity capacity, DateTimeOffset startTime)
    {
        Id = id;
        Capacity = capacity;
        StartTime = startTime;
    }

    /// <summary>The class session's id.</summary>
    public Guid Id { get; }

    /// <summary>How many members can have a reservation.</summary>
    public Capacity Capacity { get; }

    /// <summary>When the class session starts.</summary>
    public DateTimeOffset StartTime { get; }

    /// <summary>The members' seats, in the order they were reserved.</summary>
    public IReadOnlyList<Reservation> Reservations => _reservations.AsReadOnly();

    /// <summary>The waitlist entries, first come first served.</summary>
    public IReadOnlyList<WaitlistEntry> Waitlist => _waitlist.AsReadOnly();

    /// <summary>What happened to the class session, in order, for the service to publish.</summary>
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Reserves a seat for the member, unless the member already has one, the class session starts more than 4 weeks from
    /// now, or every seat is taken.
    /// </summary>
    /// <param name="memberId">The member who wants a seat.</param>
    /// <param name="clock">Tells the time now.</param>
    /// <returns>
    /// <see cref="ReservationResult.MemberAlreadyHasReservation"/>, <see cref="ReservationResult.TooFarAhead"/> or
    /// <see cref="ReservationResult.ClassSessionFull"/> when no reservation is made; then nothing changes. A member who has a
    /// seat hears that first, also on a full class session.
    /// </returns>
    public ReservationResult Reserve(Guid memberId, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (_reservations.Exists(reservation => reservation.MemberId == memberId))
        {
            return ReservationResult.MemberAlreadyHasReservation;
        }

        if (StartTime - clock.GetUtcNow() > HowFarAheadMembersCanReserve)
        {
            return ReservationResult.TooFarAhead;
        }

        if (_reservations.Count >= Capacity.Value)
        {
            return ReservationResult.ClassSessionFull;
        }

        _reservations.Add(new Reservation(memberId));
        return ReservationResult.Made;
    }

    /// <summary>Puts the member at the end of the waitlist, if every seat is taken and the member has neither a seat nor a waitlist entry.</summary>
    /// <param name="memberId">The member who wants a seat.</param>
    /// <returns>
    /// <see cref="WaitlistResult.MemberAlreadyHasReservation"/>, <see cref="WaitlistResult.MemberAlreadyOnWaitlist"/> or
    /// <see cref="WaitlistResult.ClassSessionNotFull"/> when no waitlist entry is made; then nothing changes.
    /// </returns>
    public WaitlistResult JoinWaitlist(Guid memberId)
    {
        if (_reservations.Exists(reservation => reservation.MemberId == memberId))
        {
            return WaitlistResult.MemberAlreadyHasReservation;
        }

        if (_waitlist.Exists(waitlistEntry => waitlistEntry.MemberId == memberId))
        {
            return WaitlistResult.MemberAlreadyOnWaitlist;
        }

        if (_reservations.Count < Capacity.Value)
        {
            return WaitlistResult.ClassSessionNotFull;
        }

        _waitlist.Add(new WaitlistEntry(memberId));
        return WaitlistResult.Joined;
    }

    /// <summary>Cancels the member's reservation. When someone is on the waitlist, the first waitlist entry gets the seat.</summary>
    /// <param name="memberId">The member who gives up the seat.</param>
    /// <returns><see cref="CancellationResult.NoReservation"/> when the member has no reservation; then nothing changes.</returns>
    public CancellationResult CancelReservation(Guid memberId)
    {
        int index = _reservations.FindIndex(reservation => reservation.MemberId == memberId);
        if (index < 0)
        {
            return CancellationResult.NoReservation;
        }

        _reservations.RemoveAt(index);
        _domainEvents.Add(new ReservationCancelled(Id, memberId));

        if (_waitlist.Count > 0)
        {
            WaitlistEntry firstWaitlistEntry = _waitlist[0];
            _waitlist.RemoveAt(0);
            _reservations.Add(new Reservation(firstWaitlistEntry.MemberId));
            _domainEvents.Add(new ReservationMade(Id, firstWaitlistEntry.MemberId));
        }

        return CancellationResult.Cancelled;
    }
}
