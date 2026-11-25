using BookingService.Domain;
using BookingService.Application;

namespace BookingService.Seed;

/// <summary>
/// Loads a small class schedule for the demo at start-up, when <c>Seed:Enabled</c> is true (Development and compose), and only
/// when there are no class sessions yet. Until MongoDB answers it tries again, so the service can start together with it.
/// </summary>
public sealed partial class ClassSessionSeeder : IHostedService
{
    private const int ClassSessionsPerCenterAndDay = 2;
    private const int DaysAhead = 7;
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(2);

    // The six centres and three class types of the demo. Booking knows them by id only (CONTEXT.md).
    private static readonly Guid[] CenterIds =
    [
        new("6f1c2d1e-0000-4000-8000-000000000001"),
        new("6f1c2d1e-0000-4000-8000-000000000002"),
        new("6f1c2d1e-0000-4000-8000-000000000003"),
        new("6f1c2d1e-0000-4000-8000-000000000004"),
        new("6f1c2d1e-0000-4000-8000-000000000005"),
        new("6f1c2d1e-0000-4000-8000-000000000006"),
    ];

    private static readonly Guid[] ClassTypeIds =
    [
        new("7a2b3c4d-0000-4000-8000-000000000001"), // spinning
        new("7a2b3c4d-0000-4000-8000-000000000002"), // yoga
        new("7a2b3c4d-0000-4000-8000-000000000003"), // crossfit
    ];

    private static readonly Instructor[] Instructors = [new("Mette"), new("Jonas"), new("Sara")];
    private static readonly TimeSpan ClassSessionDuration = TimeSpan.FromMinutes(60);

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<ClassSessionSeeder> _logger;

    /// <summary>Creates the seeder.</summary>
    /// <param name="scopes">Gives the seeder its own repository.</param>
    /// <param name="clock">Tells today's date.</param>
    /// <param name="logger">Logs what was loaded.</param>
    public ClassSessionSeeder(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ClassSessionSeeder> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await SeedAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (TimeoutException exception)
            {
                LogMongoDbNotReady(exception, RetryAfter.TotalSeconds);
                await Task.Delay(RetryAfter, _clock, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopes.CreateScope();
        IClassSessionRepository repository = scope.ServiceProvider.GetRequiredService<IClassSessionRepository>();
        if ((await repository.ListAsync(centerId: null, cancellationToken).ConfigureAwait(false)).Count > 0)
        {
            return;
        }

        DateTimeOffset today = new(_clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        int count = 0;
        for (int day = 1; day <= DaysAhead; day++)
        {
            foreach (Guid centerId in CenterIds)
            {
                for (int slot = 0; slot < ClassSessionsPerCenterAndDay; slot++)
                {
                    Guid classTypeId = ClassTypeIds[(day + slot) % ClassTypeIds.Length];
                    DateTimeOffset startTime = today.AddDays(day).AddHours(17 + slot);
                    var details = new ClassSessionDetails(centerId, classTypeId, Instructors[(day + slot) % Instructors.Length], ClassSessionDuration);
                    var classSession = new ClassSession(Guid.NewGuid(), new Capacity(20), startTime, details);
                    await repository.AddAsync(classSession, cancellationToken).ConfigureAwait(false);
                    count++;
                }
            }
        }

        LogSeeded(count);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "MongoDB isn't ready, so the demo class sessions aren't loaded yet. Trying again in {Seconds} s")]
    private partial void LogMongoDbNotReady(Exception exception, double seconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} demo class sessions")]
    private partial void LogSeeded(int count);
}
