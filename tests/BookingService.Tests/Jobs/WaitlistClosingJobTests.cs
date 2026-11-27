using BookingService.Application;
using BookingService.Domain;
using BookingService.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.Tests.Jobs;

/// <summary>The job that closes each waitlist 2 hours before start (#314, CONTEXT.md invariant 6).</summary>
public sealed class WaitlistClosingJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ClassSessionDetails Details = new(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60));
    // Only a guard so a broken job fails the test instead of hanging it; the job itself runs on the fake clock
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private FakeTimeProvider _clock = new(Now);

    // NUnit shares one fixture instance between tests, and the timer tests advance the clock: every test starts at Now
    [SetUp]
    public void StartAtNow()
    {
        _clock = new FakeTimeProvider(Now);
    }

    [Test]
    public async Task RunOnceAsync_WithAClassSessionStartingWithinTwoHours_ClosesItsWaitlistAndSavesIt()
    {
        // Arrange
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(1)));

        using WaitlistClosingJob job = Job(repository);

        // Act
        await job.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(repository.Saved?.Waitlist, Is.Empty);
    }

    [Test]
    public async Task RunOnceAsync_WithAClassSessionMoreThanTwoHoursAway_SavesNothing()
    {
        // Arrange
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(2).AddSeconds(1)));

        using WaitlistClosingJob job = Job(repository);

        // Act
        await job.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(repository.Saves, Is.Zero);
    }

    [Test]
    public async Task RunOnceAsync_WithAnEmptyWaitlist_SavesNothing()
    {
        // Arrange
        var repository = new StoredClassSession(() => new ClassSession(Guid.NewGuid(), new Capacity(1), Now.AddHours(1), Details));

        using WaitlistClosingJob job = Job(repository);

        // Act
        await job.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(repository.Saves, Is.Zero);
    }

    [Test]
    public async Task RunOnceAsync_WhenTheSaveLosesTheRace_ClosesTheWaitlistOnTheNextRun()
    {
        // Arrange
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(1))) { SavesToLose = 1 };
        using WaitlistClosingJob job = Job(repository);
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

    [Test]
    public async Task ExecuteAsync_AfterOneInterval_RunsAgainOnTheInjectedClock()
    {
        // Arrange: the first run loses its save, so only a second run closes the waitlist
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(1))) { SavesToLose = 1 };
        using WaitlistClosingJob job = Job(repository);
        await job.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await repository.FirstRead.WaitAsync(TestTimeout).ConfigureAwait(false);

        // Act
        _clock.Advance(WaitlistClosingJob.Interval);
        await repository.FirstSuccessfulSave.WaitAsync(TestTimeout).ConfigureAwait(false);

        // Assert
        await job.StopAsync(CancellationToken.None).ConfigureAwait(false);
        Assert.That(repository.Saved?.Waitlist, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WhenMongoDbTimesOut_KeepsRunningAndClosesOnTheNextRun()
    {
        // Arrange
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(1))) { ReadsToTimeOut = 1 };
        using WaitlistClosingJob job = Job(repository);
        await job.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await repository.FirstRead.WaitAsync(TestTimeout).ConfigureAwait(false);

        // Act
        _clock.Advance(WaitlistClosingJob.Interval);
        await repository.FirstSuccessfulSave.WaitAsync(TestTimeout).ConfigureAwait(false);

        // Assert
        await job.StopAsync(CancellationToken.None).ConfigureAwait(false);
        Assert.That(repository.Saved?.Waitlist, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WhenARunFailsWithAnyException_LogsAnErrorAndRunsAgain()
    {
        // Arrange: a broken connection is a MongoConnectionException, not a TimeoutException (#543)
        var failure = new InvalidOperationException("The connection to MongoDB broke.");
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(1))) { FirstReadFailure = failure };
        var logger = new RecordingLogger();
        using WaitlistClosingJob job = Job(repository, logger);
        await job.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await repository.FirstRead.WaitAsync(TestTimeout).ConfigureAwait(false);

        // Act
        _clock.Advance(WaitlistClosingJob.Interval);
        await repository.FirstSuccessfulSave.WaitAsync(TestTimeout).ConfigureAwait(false);

        // Assert
        await job.StopAsync(CancellationToken.None).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(logger.Errors, Is.EqualTo(new[] { failure }));
            Assert.That(repository.Saved?.Waitlist, Is.Empty);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenMongoDbTimesOut_LogsAWarningAndNoError()
    {
        // Arrange
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(1))) { ReadsToTimeOut = 1 };
        var logger = new RecordingLogger();
        using WaitlistClosingJob job = Job(repository, logger);

        // Act
        await job.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await repository.FirstRead.WaitAsync(TestTimeout).ConfigureAwait(false);
        await job.StopAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(logger.Warnings, Is.EqualTo(1));
            Assert.That(logger.Errors, Is.Empty);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenARunIsCancelled_StopsTheJob()
    {
        // Arrange: stopping the service cancels the run, and that must still end the job
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(1))) { FirstReadFailure = new OperationCanceledException() };
        using WaitlistClosingJob job = Job(repository);

        // Act
        await job.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await repository.FirstRead.WaitAsync(TestTimeout).ConfigureAwait(false);

        // Assert
        await Assert.ThatAsync(() => (job.ExecuteTask ?? Task.CompletedTask).WaitAsync(TestTimeout), Throws.InstanceOf<OperationCanceledException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task RunOnceAsync_ReadsTheClassSessionsThatStartAfterNow()
    {
        // Arrange: a later bound (now + 2 h, say) would never see a class session that is due
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddHours(1)));

        using WaitlistClosingJob job = Job(repository);

        // Act
        await job.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(repository.AskedStartsAfter, Is.EqualTo(Now));
    }

    [Test]
    public async Task RunOnceAsync_WithAClassSessionThatHasStarted_LeavesIt()
    {
        // Arrange
        var repository = new StoredClassSession(() => FullWithAWaitlistEntry(Now.AddMinutes(-1)));

        using WaitlistClosingJob job = Job(repository);

        // Act
        await job.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(repository.Saves, Is.Zero);
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
        return Job(repository, NullLogger<WaitlistClosingJob>.Instance);
    }

    private WaitlistClosingJob Job(IClassSessionRepository repository, ILogger<WaitlistClosingJob> logger)
    {
        ServiceProvider services = new ServiceCollection().AddSingleton(repository).BuildServiceProvider();
        return new WaitlistClosingJob(services.GetRequiredService<IServiceScopeFactory>(), _clock, logger);
    }

    /// <summary>Keeps the exception of every error log line and counts the warnings.</summary>
    private sealed class RecordingLogger : ILogger<WaitlistClosingJob>
    {
        public List<Exception?> Errors { get; } = [];

        public int Warnings { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Errors.Add(exception);
            }
            else if (logLevel == LogLevel.Warning)
            {
                Warnings++;
            }
        }
    }

    /// <summary>
    /// One stored class session. Every read builds a fresh copy of what is stored, as MongoDB does, so a lost save leaves the stored
    /// state as it was.
    /// </summary>
    private sealed class StoredClassSession(Func<ClassSession> build) : IClassSessionRepository
    {
        private readonly TaskCompletionSource _firstRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstSuccessfulSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _reads;

        public int SavesToLose { get; init; }

        public int ReadsToTimeOut { get; init; }

        /// <summary>When set, the first read throws it.</summary>
        public Exception? FirstReadFailure { get; init; }

        /// <summary>Completes on the first read: the job has started its timer by then.</summary>
        public Task FirstRead => _firstRead.Task;

        public Task FirstSuccessfulSave => _firstSuccessfulSave.Task;

        public int Saves { get; private set; }

        public DateTimeOffset? AskedStartsAfter { get; private set; }

        public ClassSession? Saved { get; private set; }

        public Task<IReadOnlyList<ClassSession>> ListAsync(Guid? centerId, DateTimeOffset startsAfter, CancellationToken cancellationToken)
        {
            _reads++;
            _firstRead.TrySetResult();
            if (_reads == 1 && FirstReadFailure is not null)
            {
                return Task.FromException<IReadOnlyList<ClassSession>>(FirstReadFailure);
            }

            if (_reads <= ReadsToTimeOut)
            {
                return Task.FromException<IReadOnlyList<ClassSession>>(new TimeoutException("MongoDB isn't up yet."));
            }

            // As ClassSessionRepository: only class sessions that start after startsAfter
            AskedStartsAfter = startsAfter;
            ClassSession stored = Saved ?? build();
            return Task.FromResult<IReadOnlyList<ClassSession>>(stored.StartTime > startsAfter ? [stored] : []);
        }

        public Task<ClassSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The job lists class sessions.");
        }

        public Task AddAsync(ClassSession classSession, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The job adds nothing.");
        }

        public Task SaveAsync(ClassSession classSession, CancellationToken cancellationToken)
        {
            Saves++;
            if (Saves <= SavesToLose)
            {
                return Task.FromException(new ConcurrencyConflictException());
            }

            Saved = classSession;
            _firstSuccessfulSave.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
