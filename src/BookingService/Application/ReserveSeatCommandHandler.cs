using BookingService.Domain;

namespace BookingService.Application;

/// <summary>
/// Handles <see cref="ReserveSeatCommand"/> (#260): asks Membership who the member is and whether the membership is active, then
/// lets <see cref="ClassSession.Reserve"/> decide, and saves once, only when the seat was given; then publishes ReservationMade (#192). No booking rule lives here.
/// </summary>
public sealed class ReserveSeatCommandHandler
{
    private readonly IMembershipClient _membership;
    private readonly IClassSessionRepository _classSessions;
    private readonly BookingEventPublisher _events;
    private readonly TimeProvider _clock;

    /// <summary>Creates the handler.</summary>
    /// <param name="membership">Who the member behind an account is.</param>
    /// <param name="classSessions">Where class sessions are stored.</param>
    /// <param name="events">Publishes ReservationMade after the save (#192).</param>
    /// <param name="clock">Tells the time now.</param>
    public ReserveSeatCommandHandler(IMembershipClient membership, IClassSessionRepository classSessions, BookingEventPublisher events, TimeProvider clock)
    {
        _membership = membership;
        _classSessions = classSessions;
        _events = events;
        _clock = clock;
    }

    /// <summary>Reserves a seat for the member behind the account.</summary>
    /// <param name="command">The class session, the account and the member's token.</param>
    /// <param name="cancellationToken">Stops the calls.</param>
    /// <returns>How far it got, and the class session's answer.</returns>
    public async Task<ReserveSeatResult> Handle(ReserveSeatCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        MemberLookup member = await _membership.FindMemberAsync(command.AccountId, command.BearerToken, cancellationToken).ConfigureAwait(false);
        if (member.Outcome == MemberLookupOutcome.Unavailable)
        {
            return Stopped(ReserveSeatOutcome.MembershipUnavailable, Guid.Empty);
        }

        if (member.Outcome != MemberLookupOutcome.Found || !member.IsActive)
        {
            return Stopped(ReserveSeatOutcome.MembershipNotActive, member.MemberId);
        }

        ClassSession? classSession = await _classSessions.GetByIdAsync(command.ClassSessionId, cancellationToken).ConfigureAwait(false);
        if (classSession is null)
        {
            return Stopped(ReserveSeatOutcome.ClassSessionNotFound, member.MemberId);
        }

        ReservationResult reservation = classSession.Reserve(member.MemberId, _clock);
        if (reservation == ReservationResult.Made)
        {
            if (!await _classSessions.SaveAsync(classSession, cancellationToken).ConfigureAwait(false))
            {
                return Stopped(ReserveSeatOutcome.ChangedMeanwhile, member.MemberId);
            }

            await _events.PublishReservationMadeAsync(classSession, member.MemberId).ConfigureAwait(false);
        }

        return new ReserveSeatResult(ReserveSeatOutcome.Answered, reservation, member.MemberId);
    }

    private static ReserveSeatResult Stopped(ReserveSeatOutcome outcome, Guid memberId)
    {
        // Reservation is not read unless the outcome is Answered; Made is only the enum's first value
        return new ReserveSeatResult(outcome, default, memberId);
    }
}
