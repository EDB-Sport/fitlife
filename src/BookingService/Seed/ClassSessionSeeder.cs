using BookingService.Application;
using BookingService.Domain;
using BookingService.Models;

namespace BookingService.Seed;

/// <summary>
/// Loads the demo data at start-up, when <c>Seed:Enabled</c> is true (Development and compose): six centres, three class types,
/// and two class sessions per centre and day for the next 4 weeks (#255). Every run tops the schedule up to 4 weeks from that
/// run's now and keeps what is stored, reservations included, so a restart or a redeploy refreshes it (decision on #217). Until
/// MongoDB answers it tries again, so the service can start together with it.
/// </summary>
public sealed partial class ClassSessionSeeder : IHostedService
{
    private const int ClassSessionsPerCenterAndDay = 2;

    // Days 1 to 27: the last class session still starts within 4 weeks of now, so each one can be reserved (#159)
    private const int DaysAhead = 27;
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(2);

    // The six centres and three class types of the demo. The case gives six centres but no names [PB p. 3]: made up
    private static readonly Center[] Centers =
    [
        new(new Guid("6f1c2d1e-0000-4000-8000-000000000001"), "FitLife Center 1"),
        new(new Guid("6f1c2d1e-0000-4000-8000-000000000002"), "FitLife Center 2"),
        new(new Guid("6f1c2d1e-0000-4000-8000-000000000003"), "FitLife Center 3"),
        new(new Guid("6f1c2d1e-0000-4000-8000-000000000004"), "FitLife Center 4"),
        new(new Guid("6f1c2d1e-0000-4000-8000-000000000005"), "FitLife Center 5"),
        new(new Guid("6f1c2d1e-0000-4000-8000-000000000006"), "FitLife Center 6"),
    ];

    private static readonly ClassType[] ClassTypes =
    [
        new(new Guid("7a2b3c4d-0000-4000-8000-000000000001"), "Spinning"),
        new(new Guid("7a2b3c4d-0000-4000-8000-000000000002"), "Yoga"),
        new(new Guid("7a2b3c4d-0000-4000-8000-000000000003"), "Crossfit"),
    ];

    // The last 8 bytes of every seeded class session id, so the ids can't clash with ids Guid.NewGuid() gives
    private static readonly byte[] SeededIdTail = [0x5e, 0xed, 0xde, 0x00, 0x00, 0x00, 0x02, 0x55];

    private static readonly Instructor[] Instructors = [new("Mette"), new("Jonas"), new("Sara")];
    private static readonly TimeSpan ClassSessionDuration = TimeSpan.FromMinutes(60);

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<ClassSessionSeeder> _logger;

    /// <summary>Creates the seeder.</summary>
    /// <param name="scopes">Gives the seeder its own repositories.</param>
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

    /// <summary>Stores the centres and class types, and adds every class session of the next 4 weeks that isn't stored yet.</summary>
    /// <param name="cancellationToken">Stops the seed.</param>
    internal async Task SeedAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopes.CreateScope();
        IReferenceDataWriter referenceData = scope.ServiceProvider.GetRequiredService<IReferenceDataWriter>();
        foreach (Center center in Centers)
        {
            await referenceData.SaveCenterAsync(center, cancellationToken).ConfigureAwait(false);
        }

        foreach (ClassType classType in ClassTypes)
        {
            await referenceData.SaveClassTypeAsync(classType, cancellationToken).ConfigureAwait(false);
        }

        IClassSessionRepository repository = scope.ServiceProvider.GetRequiredService<IClassSessionRepository>();
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        int added = 0;
        for (int day = 1; day <= DaysAhead; day++)
        {
            DateOnly date = today.AddDays(day);
            for (int centerIndex = 0; centerIndex < Centers.Length; centerIndex++)
            {
                for (int slot = 0; slot < ClassSessionsPerCenterAndDay; slot++)
                {
                    Guid id = SeededId(date, centerIndex, slot);
                    if (await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) is not null)
                    {
                        continue;
                    }

                    await repository.AddAsync(ClassSessionAt(id, date, centerIndex, slot), cancellationToken).ConfigureAwait(false);
                    added++;
                }
            }
        }

        LogSeeded(added);
    }

    /// <summary>The same id for the same date, centre and slot on every run, so a later run sees what an earlier one stored.</summary>
    private static Guid SeededId(DateOnly date, int centerIndex, int slot)
    {
        return new Guid(date.DayNumber, (short)centerIndex, (short)slot, SeededIdTail);
    }

    private static ClassSession ClassSessionAt(Guid id, DateOnly date, int centerIndex, int slot)
    {
        int day = date.DayNumber;
        ClassType classType = ClassTypes[(day + slot) % ClassTypes.Length];
        var startTime = new DateTimeOffset(date.ToDateTime(new TimeOnly(17 + slot, 0)), TimeSpan.Zero);
        var details = new ClassSessionDetails(Centers[centerIndex].Id, classType.Id, Instructors[(day + slot) % Instructors.Length], ClassSessionDuration);
        return new ClassSession(id, new Capacity(20), startTime, details);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "MongoDB isn't ready, so the demo class sessions aren't loaded yet. Trying again in {Seconds} s")]
    private partial void LogMongoDbNotReady(Exception exception, double seconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded the demo centres and class types, and added {Count} demo class sessions")]
    private partial void LogSeeded(int count);
}
