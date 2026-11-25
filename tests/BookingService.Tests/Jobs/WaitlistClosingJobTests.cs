using BookingService.Domain;
using BookingService.Jobs;
using BookingService.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.Tests.Jobs;

/// <summary>The job that closes each waitlist 2 hours before start (#314, CONTEXT.md invariant 6).</summary>
public sealed class WaitlistClosingJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ClassSessionDetails Details = new(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60));
    private readonly FakeTimeProvider _clock = new(Now);

    [Test]
    public async Task RunOnceAsync_WithAClassSessionStartingWithinTwoHours_ClosesItsWaitlistAndSavesIt()
    {
        // Arrange
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(1)));

        // Act
        await Job(repository).RunOnceAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(repository.Saved?.Waitlist, Is.Empty);
    }

    [Test]
    public async Task RunOnceAsync_WithAClassSessionMoreThanTwoHoursAway_SavesNothing()
    {
        // Arrange
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(2).AddSeconds(1)));

        // Act
        await Job(repository).RunOnceAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(repository.Saves, Is.Zero);
    }

    [Test]
    public async Task RunOnceAsync_WithAnEmptyWaitlist_SavesNothing()
    {
        // Arrange
        var repository = new StoredClassSession(() => new ClassSession(Guid.NewGuid(), new Capacity(1), Now.AddHours(1), Details));

        // Act
        await Job(repository).RunOnceAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(repository.Saves, Is.Zero);
    }

    [Test]
    public async Task RunOnceAsync_WhenTheSaveLosesTheRace_ClosesTheWaitlistOnTheNextRun()
    {
        // Arrange
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(1))) { SavesToLose = 1 };
        WaitlistClosingJob job = Job(repository);
        await job.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);

        // Act
        await job.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(repository.Saves, Is.EqualTo(2));
            Assert.That(repository.Saved?.Waitlist, Is.Empty);
        });
    }

    /// <summary>A full class session with one waitlist entry, joined while the waitlist was still open.</summary>
    private static ClassSession FullWithAWaitlistEntry(DateTimeOffset startTime)
    {
        var earlier = new FakeTimeProvider(startTime.AddDays(-1));
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(1), startTime, Details);
        classSession.Reserve(Guid.NewGuid(), earlier);
        classSession.JoinWaitlist(Guid.NewGuid(), earlier);
        return classSession;
    }

    private WaitlistClosingJob Job(IClassSessionRepository repository)
    {
        ServiceProvider services = new ServiceCollection().AddSingleton(repository).BuildServiceProvider();
        return new WaitlistClosingJob(services.GetRequiredService<IServiceScopeFactory>(), _clock, NullLogger<WaitlistClosingJob>.Instance);
    }

    /// <summary>
    /// One stored class session. Every read builds a fresh copy of what is stored, as MongoDB does, so a lost save leaves the stored
    /// state as it was.
    /// </summary>
    private sealed class StoredClassSession(Func<ClassSession> build) : IClassSessionRepository
    {
        public int SavesToLose { get; init; }

        public int Saves { get; private set; }

        public ClassSession? Saved { get; private set; }

        public Task<IReadOnlyList<ClassSession>> ListAsync(Guid? centerId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ClassSession>>([Saved ?? build()]);
        }

        public Task<ClassSession?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The job lists class sessions.");
        }

        public Task AddAsync(ClassSession classSession, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The job adds nothing.");
        }

        public Task<bool> SaveAsync(ClassSession classSession, CancellationToken cancellationToken)
        {
            Saves++;
            if (Saves <= SavesToLose)
            {
                return Task.FromResult(false);
            }

            Saved = classSession;
            return Task.FromResult(true);
        }
    }
}
