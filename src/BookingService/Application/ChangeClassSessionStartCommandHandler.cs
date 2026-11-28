using BookingService.Domain;

namespace BookingService.Application;

/// <summary>
/// Handles <see cref="ChangeClassSessionStartCommand"/> (#241): lets <see cref="ClassSession.ChangeStart"/> move the class session,
/// and saves once. A start the class session refuses is not saved.
/// </summary>
public sealed class ChangeClassSessionStartCommandHandler
{
    private readonly IClassSessionRepository _classSessions;
    private readonly TimeProvider _clock;

    /// <summary>Creates the handler.</summary>
    /// <param name="classSessions">Where class sessions are stored.</param>
    /// <param name="clock">Tells the time now.</param>
    public ChangeClassSessionStartCommandHandler(IClassSessionRepository classSessions, TimeProvider clock)
    {
        _classSessions = classSessions;
        _clock = clock;
    }

    /// <summary>Moves the class session to the new start.</summary>
    /// <param name="command">The class session and its new start.</param>
    /// <param name="cancellationToken">Stops the calls.</param>
    /// <returns>Whether it was changed, or why nothing was saved.</returns>
    public async Task<ChangeClassSessionStartOutcome> Handle(ChangeClassSessionStartCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ClassSession? classSession = await _classSessions.GetByIdAsync(command.ClassSessionId, cancellationToken).ConfigureAwait(false);
        if (classSession is null)
        {
            return ChangeClassSessionStartOutcome.ClassSessionNotFound;
        }

        switch (classSession.ChangeStart(command.NewStart, _clock))
        {
            case ChangeStartResult.ClassSessionCancelled:
                return ChangeClassSessionStartOutcome.ClassSessionCancelled;
            case ChangeStartResult.StartNotInTheFuture:
                return ChangeClassSessionStartOutcome.StartNotInTheFuture;
            case ChangeStartResult.ClassSessionStarted:
                return ChangeClassSessionStartOutcome.ClassSessionStarted;
            default:
                break;
        }

        if (!await _classSessions.SaveAsync(classSession, cancellationToken).ConfigureAwait(false))
        {
            return ChangeClassSessionStartOutcome.ChangedMeanwhile;
        }

        return ChangeClassSessionStartOutcome.Changed;
    }
}
