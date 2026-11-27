using BookingService.Application;
using BookingService.Domain;

namespace BookingService.Jobs;

/// <summary>
/// Closes the waitlist of every upcoming class session that starts within 2 hours, once a minute (#314). It runs when
/// <c>WaitlistClosing:Enabled</c> is true. A save that loses to another request is not lost: the next run reads the class
/// session again and closes it then.
/// </summary>
public sealed partial class WaitlistClosingJob : BackgroundService
{
    /// <summary>How often the job runs.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<WaitlistClosingJob> _logger;

    /// <summary>Creates the job.</summary>
    /// <param name="scopes">Gives each run its own repository.</param>
    /// <param name="clock">Tells the time now and paces the runs.</param>
    /// <param name="logger">Logs what was closed.</param>
    public WaitlistClosingJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<WaitlistClosingJob> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>One run: closes and saves every waitlist that is due.</summary>
    /// <param name="cancellationToken">Stops the run.</param>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopes.CreateScope();
        IClassSessionRepository repository = scope.ServiceProvider.GetRequiredService<IClassSessionRepository>();
        foreach (ClassSession classSession in await repository.ListAsync(centerId: null, startsAfter: _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            if (classSession.Waitlist.Count == 0 || classSession.CloseWaitlist(_clock) != WaitlistClosingResult.Closed)
            {
                continue;
            }

            if (await repository.SaveAsync(classSession, cancellationToken).ConfigureAwait(false))
            {
                LogClosed(classSession.Id);
            }
            else
            {
                LogLostRace(classSession.Id);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, _clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                LogMongoDbNotReady(exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Closed the waitlist of class session {ClassSessionId}")]
    private partial void LogClosed(Guid classSessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Class session {ClassSessionId} changed at the same time; its waitlist is closed on the next run")]
    private partial void LogLostRace(Guid classSessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MongoDB isn't ready, so no waitlist was closed this run")]
    private partial void LogMongoDbNotReady(Exception exception);
}
