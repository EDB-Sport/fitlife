using BookingService.Domain;

namespace BookingService.Application;

/// <summary>
/// Handles <see cref="CancelClassSessionCommand"/> (#239): lets <see cref="ClassSession.Cancel"/> empty the reservations and the
/// waitlist, and saves once. <see cref="ClassSession.Cancel"/> doesn't refuse a second call, so the handler checks
/// <see cref="ClassSession.IsCancelled"/> first and then neither calls it nor saves. A refused save throws
/// <see cref="ConcurrencyConflictException"/>, which the API answers with 409 <c>ConcurrentChange</c> (#245).
/// </summary>
public sealed class CancelClassSessionCommandHandler
{
    private readonly IClassSessionRepository _classSessions;

    /// <summary>Creates the handler.</summary>
    /// <param name="classSessions">Where class sessions are stored.</param>
    public CancelClassSessionCommandHandler(IClassSessionRepository classSessions)
    {
        _classSessions = classSessions;
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

        if (classSession.IsCancelled)
        {
            return CancelClassSessionOutcome.ClassSessionAlreadyCancelled;
        }

        classSession.Cancel();
        await _classSessions.SaveAsync(classSession, cancellationToken).ConfigureAwait(false);
        return CancelClassSessionOutcome.Cancelled;
    }
}
