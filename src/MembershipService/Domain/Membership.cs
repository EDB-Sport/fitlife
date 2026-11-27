namespace MembershipService.Domain;

/// <summary>Medlemskab: a member's agreement with FitLife. Refers to its <see cref="Member"/> by id only.</summary>
public sealed class Membership
{
    private Membership(Guid id, Guid memberId, MembershipType type, MembershipStatus status, DateOnly startDate)
    {
        Id = id;
        MemberId = memberId;
        Type = type;
        Status = status;
        StartDate = startDate;
    }

    /// <summary>The membership id.</summary>
    public Guid Id { get; }

    /// <summary>The member the membership belongs to.</summary>
    public Guid MemberId { get; }

    /// <summary>The kind of membership.</summary>
    public MembershipType Type { get; }

    /// <summary>
    /// The stored status: <see cref="MembershipStatus.Active"/>, <see cref="MembershipStatus.PaymentMissing"/> or
    /// <see cref="MembershipStatus.Cancelled"/>. It doesn't look at the dates; the status on a given day is <see cref="StatusOn"/>.
    /// </summary>
    public MembershipStatus Status { get; private set; }

    /// <summary>The day the membership started.</summary>
    public DateOnly StartDate { get; }

    /// <summary>The first day of the pause period, both days included; null without one.</summary>
    public DateOnly? PausedFrom { get; private set; }

    /// <summary>The last day of the pause period; null without one.</summary>
    public DateOnly? PausedTo { get; private set; }

    /// <summary>The last day the membership gives access, after the member cancelled it (UC8); null when it isn't cancelled.</summary>
    public DateOnly? LastTrainingDay { get; private set; }

    /// <summary>Whether the stored status is <see cref="MembershipStatus.Active"/>. A pause period isn't counted: use <see cref="IsActiveOn"/>.</summary>
    public bool IsActive => Status == MembershipStatus.Active;

    /// <summary>
    /// A new membership starts the day the member joins, with Betaling mangler until the first fee is paid
    /// (<see cref="MarkPaid"/>), so an interrupted join never gives access without a paid fee (TO-BE-kontekst §8, UC5).
    /// </summary>
    /// <param name="memberId">The member who joined.</param>
    /// <param name="startDate">Today.</param>
    /// <returns>The new membership.</returns>
    public static Membership Start(Guid memberId, DateOnly startDate)
    {
        return new Membership(Guid.NewGuid(), memberId, MembershipType.Standard, MembershipStatus.PaymentMissing, startDate);
    }

    /// <summary>The membership fee was paid: the membership gives access and the right to book.</summary>
    public void MarkPaid()
    {
        Status = MembershipStatus.Active;
    }

    /// <summary>
    /// Where the membership is on a day: Afsluttet after the <see cref="LastTrainingDay"/> of a cancelled membership; Pause from
    /// <see cref="PausedFrom"/> to <see cref="PausedTo"/>, both included, when the stored status is Aktiv; else the stored status
    /// (TO-BE-kontekst §8). A cancelled membership is Opsagt inside a pause period too. No background job: the dates decide.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>The status on that day.</returns>
    public MembershipStatus StatusOn(DateOnly day)
    {
        if (Status == MembershipStatus.Cancelled && day > LastTrainingDay)
        {
            return MembershipStatus.Ended;
        }

        if (Status != MembershipStatus.Active)
        {
            return Status;
        }

        if (PausedFrom is { } from && PausedTo is { } to && from <= day && day <= to)
        {
            return MembershipStatus.Paused;
        }

        return MembershipStatus.Active;
    }

    /// <summary>Whether the membership gives access and the right to book on a day.</summary>
    /// <param name="day">The day.</param>
    /// <returns>True when the status on that day is Aktiv.</returns>
    public bool IsActiveOn(DateOnly day)
    {
        return StatusOn(day) == MembershipStatus.Active;
    }

    /// <summary>
    /// Pauses the membership from <paramref name="from"/> to <paramref name="to"/>, both included (UC6). There is one pause
    /// period: a new one replaces one still to come. The rules are checked in the order of <see cref="PauseResult"/>.
    /// </summary>
    /// <param name="from">The first day of the pause.</param>
    /// <param name="to">The last day of the pause, after <paramref name="from"/>.</param>
    /// <param name="today">Today.</param>
    /// <returns><see cref="PauseResult.Paused"/>, or why the pause was rejected.</returns>
    public PauseResult Pause(DateOnly from, DateOnly to, DateOnly today)
    {
        if (StatusOn(today) != MembershipStatus.Active)
        {
            return PauseResult.MembershipNotActive;
        }

        if (from < today)
        {
            return PauseResult.PauseStartsInThePast;
        }

        if (to <= from)
        {
            return PauseResult.PauseEndsBeforeItStarts;
        }

        PausedFrom = from;
        PausedTo = to;
        return PauseResult.Paused;
    }

    /// <summary>
    /// Ends the pause early (UC6): a pause period that isn't over on <paramref name="today"/>, running or still to come, is
    /// removed, so the membership is Aktiv from today.
    /// </summary>
    /// <param name="today">Today.</param>
    /// <returns><see cref="EndPauseResult.PauseEnded"/>, or <see cref="EndPauseResult.NoPauseToEnd"/>.</returns>
    public EndPauseResult EndPause(DateOnly today)
    {
        if (PausedTo is not { } to || to < today)
        {
            return EndPauseResult.NoPauseToEnd;
        }

        PausedFrom = null;
        PausedTo = null;
        return EndPauseResult.PauseEnded;
    }

    /// <summary>
    /// The member cancels the membership with one month's notice from the first day of the coming month (TO-BE-kontekst §8,
    /// UC8): the last training day is the last day of the month after <paramref name="today"/>'s month, and the membership is
    /// Opsagt until then. Aktiv, Pause and Betaling mangler can all be cancelled.
    /// </summary>
    /// <param name="today">Today.</param>
    /// <returns><see cref="CancelResult.Cancelled"/>, or <see cref="CancelResult.MembershipAlreadyCancelled"/>.</returns>
    public CancelResult Cancel(DateOnly today)
    {
        if (StatusOn(today) is MembershipStatus.Cancelled or MembershipStatus.Ended)
        {
            return CancelResult.MembershipAlreadyCancelled;
        }

        // The first day of today's month plus two months, minus one day: the year changes by itself (Dec, Jan)
        LastTrainingDay = new DateOnly(today.Year, today.Month, 1).AddMonths(2).AddDays(-1);
        Status = MembershipStatus.Cancelled;
        return CancelResult.Cancelled;
    }

    /// <summary>
    /// The member paid the missing fee: Betaling mangler becomes Aktiv (TO-BE-kontekst §8, UC18). Any other status owes nothing,
    /// and nothing changes.
    /// </summary>
    /// <returns><see cref="PaymentResult.Paid"/>, or <see cref="PaymentResult.NothingIsOwed"/>.</returns>
    public PaymentResult RegisterPayment()
    {
        if (Status != MembershipStatus.PaymentMissing)
        {
            return PaymentResult.NothingIsOwed;
        }

        MarkPaid();
        return PaymentResult.Paid;
    }

    /// <summary>
    /// Rebuilds a stored membership. A stored pause period is given back with <see cref="RestorePausePeriod"/>, a stored last
    /// training day with <see cref="RestoreLastTrainingDay"/>.
    /// </summary>
    /// <param name="id">The membership id.</param>
    /// <param name="memberId">The member id.</param>
    /// <param name="type">The kind of membership.</param>
    /// <param name="status">The stored status.</param>
    /// <param name="startDate">The start date.</param>
    /// <returns>The membership.</returns>
    public static Membership Restore(Guid id, Guid memberId, MembershipType type, MembershipStatus status, DateOnly startDate)
    {
        return new Membership(id, memberId, type, status, startDate);
    }

    /// <summary>
    /// Gives a restored membership back its stored pause period, without the rules of <see cref="Pause"/>: they were checked
    /// when it was stored. Only the repository calls it, while it rebuilds a membership.
    /// </summary>
    /// <param name="pausedFrom">The stored first day, or null.</param>
    /// <param name="pausedTo">The stored last day, or null.</param>
    internal void RestorePausePeriod(DateOnly? pausedFrom, DateOnly? pausedTo)
    {
        PausedFrom = pausedFrom;
        PausedTo = pausedTo;
    }

    /// <summary>
    /// Gives a restored membership back its stored last training day, without the rules of <see cref="Cancel"/>: they were
    /// checked when it was stored. Only the repository calls it, while it rebuilds a membership.
    /// </summary>
    /// <param name="lastTrainingDay">The stored last training day, or null.</param>
    internal void RestoreLastTrainingDay(DateOnly? lastTrainingDay)
    {
        LastTrainingDay = lastTrainingDay;
    }
}
