using BookingService.Domain;

namespace BookingService.Application;

/// <summary>
/// Handles <see cref="CancelReservationCommand"/> (#262): asks Membership who the member is, then lets
/// <see cref="ClassSession.CancelReservation"/> decide, and saves once, only when it cancelled. Whether the membership is active
/// doesn't matter: a paused member may still cancel. Who gets the seat and whether it is late are the aggregate's rules.
/// </summary>
public sealed class CancelReservationCommandHandler
{
    private readonly IMembershipClient _membership;
    private readonly IClassSessionRepository _classSessions;
    private readonly TimeProvider _clock;

    /// <summary>Creates the handler.</summary>
    /// <param name="membership">Who the member behind an account is.</param>
    /// <param name="classSessions">Where class sessions are stored.</param>
    /// <param name="clock">Tells the time now.</param>
    public CancelReservationCommandHandler(IMembershipClient membership, IClassSessionRepository classSessions, TimeProvider clock)
    {
        _membership = membership;
        _classSessions = classSessions;
        _clock = clock;
    }

    /// <summary>Cancels the reservation of the member behind the account.</summary>
    /// <param name="command">The class session, the account and the member's token.</param>
    /// <param name="cancellationToken">Stops the calls.</param>
    /// <returns>How it went.</returns>
    public async Task<CancelReservationOutcome> Handle(CancelReservationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        MemberLookup member = await _membership.FindMemberAsync(command.AccountId, command.BearerToken, cancellationToken).ConfigureAwait(false);
        if (member.Outcome == MemberLookupOutcome.Unavailable)
        {
            return CancelReservationOutcome.MembershipUnavailable;
        }

        if (member.Outcome != MemberLookupOutcome.Found)
        {
            return CancelReservationOutcome.NoReservation;
        }

        ClassSession? classSession = await _classSessions.GetByIdAsync(command.ClassSessionId, cancellationToken).ConfigureAwait(false);
        if (classSession is null)
        {
            return CancelReservationOutcome.ClassSessionNotFound;
        }

        if (classSession.CancelReservation(member.MemberId, _clock) == CancellationResult.NoReservation)
        {
            return CancelReservationOutcome.NoReservation;
        }

        if (!await _classSessions.SaveAsync(classSession, cancellationToken).ConfigureAwait(false))
        {
            return CancelReservationOutcome.ChangedMeanwhile;
        }

        return CancelReservationOutcome.Cancelled;
    }
}
