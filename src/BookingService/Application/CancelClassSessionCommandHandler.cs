using BookingService.Domain;

namespace BookingService.Application;

/// <summary>
/// Handles <see cref="CancelClassSessionCommand"/> (#239): lets <see cref="ClassSession.Cancel"/> empty the reservations and the
/// waitlist, and saves once. A class session that is already cancelled or has started (#538) is not saved.
/// </summary>
public sealed class CancelClassSessionCommandHandler
{
    private readonly IClassSessionRepository _classSessions;
    private readonly TimeProvider _clock;

    /// <summary>Creates the handler.</summary>
    /// <param name="classSessions">Where class sessions are stored.</param>
    /// <param name="clock">Tells the time now.</param>
    public CancelClassSessionCommandHandler(IClassSessionRepository classSessions, TimeProvider clock)
    {
        _classSessions = classSessions;
        _clock = clock;
    }

    /// <summary>Cancels the class session.</summary>
    /// <param name="command">The class session.</param>
    /// <param name="cancellationToken">Stops the calls.</param>
    /// <returns>Whether it was cancelled, or why nothing was saved.</returns>
    public async Task<CancelClassSessionOutcome> Handle(CancelClassSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ClassSession? classSession = await _classSessions.GetByIdAsync(command.ClassSessionId, cancellationToken).ConfigureAwait(false);
        if (classSession is null)
        {
            return CancelClassSessionOutcome.ClassSessionNotFound;
        }

        switch (classSession.Cancel(_clock))
        {
            case CancelResult.AlreadyCancelled:
                return CancelClassSessionOutcome.ClassSessionAlreadyCancelled;
            case CancelResult.ClassSessionStarted:
                return CancelClassSessionOutcome.ClassSessionStarted;
            default:
                break;
        }

        if (!await _classSessions.SaveAsync(classSession, cancellationToken).ConfigureAwait(false))
        {
            return CancelClassSessionOutcome.ChangedMeanwhile;
        }

        return CancelClassSessionOutcome.Cancelled;
    }
}
