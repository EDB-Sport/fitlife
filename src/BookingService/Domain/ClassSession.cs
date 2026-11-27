namespace BookingService.Domain;

/// <summary>
/// Hold: one scheduled run of a class (GLOSSARY.md). The aggregate root of Booking: reservations and waitlist entries change
/// only through it, so it guards every invariant in src/BookingService/CONTEXT.md.
/// </summary>
public sealed class ClassSession
{
    private readonly List<Reservation> _reservations = [];
    private readonly List<WaitlistEntry> _waitlist = [];
    private readonly List<DomainEvent> _domainEvents = [];

    private static readonly TimeSpan HowFarAheadMembersCanReserve = TimeSpan.FromDays(28);
    private static readonly TimeSpan WhenTheWaitlistClosesBeforeStart = TimeSpan.FromHours(2);
    private static readonly TimeSpan WhenACancellationIsLateBeforeStart = TimeSpan.FromHours(2);

    /// <summary>Creates a class session with no reservations.</summary>
    /// <param name="id">The class session's id.</param>
    /// <param name="capacity">How many members can have a reservation.</param>
    /// <param name="startTime">When the class session starts.</param>
    /// <param name="details">Its centre, class type, instructor and duration (Holdoplysninger).</param>
    public ClassSession(Guid id, Capacity capacity, DateTimeOffset startTime, ClassSessionDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        Id = id;
        Capacity = capacity;
        StartTime = startTime;
        Details = details;
    }

    /// <summary>The class session's id.</summary>
    public Guid Id { get; }

    /// <summary>The stored version this copy was read at or saved as, set by the repository; 0 before it is stored.</summary>
    public int Version { get; internal set; }

    /// <summary>Its centre, class type, instructor and duration (Holdoplysninger).</summary>
    public ClassSessionDetails Details { get; }

    /// <summary>The centre the class session takes place in.</summary>
    public Guid CenterId => Details.CenterId;

    /// <summary>The kind of class (Holdtype) this is a run of.</summary>
    public Guid ClassTypeId => Details.ClassTypeId;

    /// <summary>How many members can have a reservation.</summary>
    public Capacity Capacity { get; }

    /// <summary>When the class session starts.</summary>
    public DateTimeOffset StartTime { get; private set; }

    /// <summary>The class session will not take place (Hold aflyst).</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>How many seats are not reserved yet. A cancelled class session has none.</summary>
    public int FreeSeats => IsCancelled ? 0 : Capacity.Value - _reservations.Count;

    /// <summary>The members' seats, in the order they were reserved.</summary>
    public IReadOnlyList<Reservation> Reservations => _reservations.AsReadOnly();

    /// <summary>The waitlist entries, first come first served.</summary>
    public IReadOnlyList<WaitlistEntry> Waitlist => _waitlist.AsReadOnly();

    /// <summary>What happened to the class session, in order, for the service to publish.</summary>
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Reserves a seat for the member, unless the member already has one, the class session has started or starts more than
    /// 4 weeks from now, or every seat is taken.
    /// </summary>
    /// <param name="memberId">The member who wants a seat.</param>
    /// <param name="clock">Tells the time now.</param>
    /// <returns>
    /// <see cref="ReservationResult.ClassSessionCancelled"/>, <see cref="ReservationResult.MemberAlreadyHasReservation"/>,
    /// <see cref="ReservationResult.ClassSessionStarted"/>, <see cref="ReservationResult.TooFarAhead"/> or
    /// <see cref="ReservationResult.ClassSessionFull"/> when no reservation is made; then nothing changes. A member who has a
    /// seat hears that first, also on a full class session.
    /// </returns>
    public ReservationResult Reserve(Guid memberId, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (IsCancelled)
        {
            return ReservationResult.ClassSessionCancelled;
        }

        if (_reservations.Exists(reservation => reservation.MemberId == memberId))
        {
            return ReservationResult.MemberAlreadyHasReservation;
        }

        if (clock.GetUtcNow() >= StartTime)
        {
            return ReservationResult.ClassSessionStarted;
        }

        if (StartTime - clock.GetUtcNow() > HowFarAheadMembersCanReserve)
        {
            return ReservationResult.TooFarAhead;
        }

        if (_reservations.Count >= Capacity.Value)
        {
            return ReservationResult.ClassSessionFull;
        }

        _reservations.Add(new Reservation(memberId, isFromWaitlist: false));
        return ReservationResult.Made;
    }

    /// <summary>
    /// Puts the member at the end of the waitlist, if the class session isn't cancelled, every seat is taken, the waitlist is still open, and the member has
    /// neither a seat nor a waitlist entry.
    /// </summary>
    /// <param name="memberId">The member who wants a seat.</param>
    /// <param name="clock">Tells the time now.</param>
    /// <returns>
    /// <see cref="WaitlistResult.ClassSessionCancelled"/>, <see cref="WaitlistResult.MemberAlreadyHasReservation"/>,
    /// <see cref="WaitlistResult.MemberAlreadyOnWaitlist"/>,
    /// <see cref="WaitlistResult.ClassSessionNotFull"/> when a seat is free, or <see cref="WaitlistResult.WaitlistClosed"/>
    /// from 2 hours before start; then nothing changes.
    /// </returns>
    public WaitlistResult JoinWaitlist(Guid memberId, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (IsCancelled)
        {
            return WaitlistResult.ClassSessionCancelled;
        }

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

        // At exactly 2 hours the waitlist is closed: the instant CloseWaitlist may run (#314)
        if (StartTime - clock.GetUtcNow() <= WhenTheWaitlistClosesBeforeStart)
        {
            return WaitlistResult.WaitlistClosed;
        }

        _waitlist.Add(new WaitlistEntry(memberId));
        return WaitlistResult.Joined;
    }

    /// <summary>
    /// Cancels the member's reservation. Less than 2 hours before start it is a late cancellation, unless the seat came from the
    /// waitlist. When someone is on the waitlist, the first waitlist entry gets the seat.
    /// </summary>
    /// <param name="memberId">The member who gives up the seat.</param>
    /// <param name="clock">Tells the time now.</param>
    /// <returns><see cref="CancellationResult.NoReservation"/> when the member has no reservation; then nothing changes.</returns>
    public CancellationResult CancelReservation(Guid memberId, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        int index = _reservations.FindIndex(reservation => reservation.MemberId == memberId);
        if (index < 0)
        {
            return CancellationResult.NoReservation;
        }

        Reservation reservation = _reservations[index];
        bool isLateCancellation = !reservation.IsFromWaitlist
            && !reservation.IsFromBeforeAChangedStart
            && StartTime - clock.GetUtcNow() < WhenACancellationIsLateBeforeStart;
        _reservations.RemoveAt(index);
        _domainEvents.Add(new ReservationCancelled(Id, memberId, isLateCancellation));

        if (_waitlist.Count > 0)
        {
            WaitlistEntry firstWaitlistEntry = _waitlist[0];
            _waitlist.RemoveAt(0);
            _reservations.Add(new Reservation(firstWaitlistEntry.MemberId, isFromWaitlist: true));
            _domainEvents.Add(new ReservationMade(Id, firstWaitlistEntry.MemberId));
        }

        return CancellationResult.Cancelled;
    }

    /// <summary>
    /// Closes the waitlist 2 hours before start: every remaining waitlist entry is removed without a seat, and each member gets a
    /// <see cref="WaitlistEntryExpired"/> event.
    /// </summary>
    /// <param name="clock">Tells the time now.</param>
    /// <returns><see cref="WaitlistClosingResult.TooEarly"/> more than 2 hours before start; then nothing changes.</returns>
    public WaitlistClosingResult CloseWaitlist(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (StartTime - clock.GetUtcNow() > WhenTheWaitlistClosesBeforeStart)
        {
            return WaitlistClosingResult.TooEarly;
        }

        foreach (WaitlistEntry waitlistEntry in _waitlist)
        {
            _domainEvents.Add(new WaitlistEntryExpired(Id, waitlistEntry.MemberId));
        }

        _waitlist.Clear();
        return WaitlistClosingResult.Closed;
    }

    /// <summary>
    /// Moves the class session to a new start. Its reservations and waitlist entries are kept, and a member who has a reservation
    /// now can later cancel it free: never a late cancellation. <see cref="ClassSessionChanged"/> names those members.
    /// </summary>
    /// <param name="newStart">When the class session starts from now on.</param>
    /// <param name="clock">Tells the time now.</param>
    /// <returns>
    /// <see cref="ChangeStartResult.ClassSessionCancelled"/>, <see cref="ChangeStartResult.ClassSessionStarted"/> or
    /// <see cref="ChangeStartResult.StartNotInTheFuture"/> when the start doesn't change; then nothing changes and nothing is raised.
    /// </returns>
    public ChangeStartResult ChangeStart(DateTimeOffset newStart, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (IsCancelled)
        {
            return ChangeStartResult.ClassSessionCancelled;
        }

        if (HasStarted(clock))
        {
            return ChangeStartResult.ClassSessionStarted;
        }

        if (newStart <= clock.GetUtcNow())
        {
            return ChangeStartResult.StartNotInTheFuture;
        }

        DateTimeOffset previousStart = StartTime;
        StartTime = newStart;
        for (int index = 0; index < _reservations.Count; index++)
        {
            _reservations[index] = _reservations[index].FromBeforeAChangedStart();
        }

        var memberIds = _reservations.Select(reservation => reservation.MemberId).ToList();
        _domainEvents.Add(new ClassSessionChanged(Id, memberIds.AsReadOnly(), previousStart, newStart));
        return ChangeStartResult.Changed;
    }

    /// <summary>
    /// Cancels the class session: every reservation and waitlist entry is removed, and <see cref="ClassSessionCancelled"/> names
    /// their members. A cancelled class session can't be reserved. Cancelling it again, or once it has started (from the start
    /// on, #538), changes nothing and raises no event.
    /// </summary>
    /// <param name="clock">Tells the time now.</param>
    /// <returns><see cref="CancelResult.Cancelled"/>, or why nothing changed.</returns>
    public CancelResult Cancel(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (IsCancelled)
        {
            return CancelResult.AlreadyCancelled;
        }

        if (HasStarted(clock))
        {
            return CancelResult.ClassSessionStarted;
        }

        var memberIds = _reservations.Select(reservation => reservation.MemberId)
            .Concat(_waitlist.Select(waitlistEntry => waitlistEntry.MemberId))
            .ToList();
        _reservations.Clear();
        _waitlist.Clear();
        IsCancelled = true;
        _domainEvents.Add(new ClassSessionCancelled(Id, memberIds.AsReadOnly()));
        return CancelResult.Cancelled;
    }

    // The start is the cut-off (#312): from that moment on the class session has started
    private bool HasStarted(TimeProvider clock)
    {
        return clock.GetUtcNow() >= StartTime;
    }

    /// <summary>
    /// Puts back the state a repository stored. Only for a class session just created with the constructor; it raises no
    /// events and checks no rule, because the stored state already passed them.
    /// </summary>
    /// <param name="isCancelled">Whether the class session was cancelled.</param>
    /// <param name="reservations">The reservations, in the order they were made.</param>
    /// <param name="waitlist">The waitlist entries, first come first served.</param>
    internal void Restore(bool isCancelled, IEnumerable<Reservation> reservations, IEnumerable<WaitlistEntry> waitlist)
    {
        IsCancelled = isCancelled;
        _reservations.AddRange(reservations);
        _waitlist.AddRange(waitlist);
    }
}
