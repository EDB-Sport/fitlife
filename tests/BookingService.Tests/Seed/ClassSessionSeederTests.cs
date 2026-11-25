using BookingService.Application;
using BookingService.Domain;
using BookingService.Models;
using BookingService.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.Tests.Seed;

/// <summary>The demo data: six centres, class types, and class sessions that can be reserved for the next 4 weeks (#255).</summary>
public sealed class ClassSessionSeederTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan FourWeeks = TimeSpan.FromDays(28);

    private FakeTimeProvider _clock = new(Now);
    private StoredClassSessions _classSessions = new();
    private StoredReferenceData _referenceData = new();

    [SetUp]
    public void StartWithAnEmptyDatabase()
    {
        _clock = new FakeTimeProvider(Now);
        _classSessions = new StoredClassSessions();
        _referenceData = new StoredReferenceData();
    }

    [Test]
    public async Task SeedAsync_OnAnEmptyDatabase_StoresTheDemoData()
    {
        // Arrange
        ClassSessionSeeder seeder = Seeder();

        // Act
        await seeder.SeedAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_referenceData.Centers.Values.Select(center => center.Name).Order(StringComparer.Ordinal), Is.EqualTo(Enumerable.Range(1, 6).Select(number => $"FitLife Center {number}")));
            Assert.That(_referenceData.ClassTypes, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(_referenceData.Centers.Keys, Is.SubsetOf(_classSessions.All.Select(classSession => classSession.CenterId).Distinct()));
            Assert.That(_referenceData.Centers.Keys, Is.SupersetOf(_classSessions.All.Select(classSession => classSession.CenterId).Distinct()));
            Assert.That(_classSessions.All.Select(classSession => classSession.ClassTypeId).Distinct(), Is.SubsetOf(_referenceData.ClassTypes.Keys));
        });
    }

    [Test]
    public async Task SeedAsync_OnAnEmptyDatabase_StoresOnlyClassSessionsThatCanBeReserved()
    {
        // Arrange
        ClassSessionSeeder seeder = Seeder();

        // Act
        await seeder.SeedAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_classSessions.All, Is.Not.Empty);
            Assert.That(_classSessions.All.Select(classSession => classSession.Reserve(Guid.NewGuid(), _clock)), Is.All.EqualTo(ReservationResult.Made));
        });
    }

    [Test]
    public async Task SeedAsync_RunTwice_LeavesTheSameDemoDataAndKeepsReservations()
    {
        // Arrange
        ClassSessionSeeder seeder = Seeder();
        await seeder.SeedAsync(CancellationToken.None).ConfigureAwait(false);
        ClassSession reserved = _classSessions.All[0];
        reserved.Reserve(Guid.NewGuid(), _clock);
        Guid[] idsBefore = [.. _classSessions.All.Select(classSession => classSession.Id)];

        // Act
        await seeder.SeedAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_classSessions.All.Select(classSession => classSession.Id), Is.EquivalentTo(idsBefore));
            Assert.That(_classSessions.Get(reserved.Id)?.Reservations, Has.Count.EqualTo(1));
            Assert.That(_referenceData.Centers, Has.Count.EqualTo(6));
        });
    }

    [Test]
    public async Task SeedAsync_AWeekLater_TopsTheScheduleUpToFourWeeksFromThenAndKeepsWhatExists()
    {
        // Arrange
        ClassSessionSeeder seeder = Seeder();
        await seeder.SeedAsync(CancellationToken.None).ConfigureAwait(false);
        Guid[] idsBefore = [.. _classSessions.All.Select(classSession => classSession.Id)];
        DateTimeOffset lastStartBefore = _classSessions.All.Max(classSession => classSession.StartTime);
        _clock.Advance(TimeSpan.FromDays(7));

        // Act
        await seeder.SeedAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        DateTimeOffset lastStart = _classSessions.All.Max(classSession => classSession.StartTime);
        Assert.Multiple(() =>
        {
            Assert.That(_classSessions.All.Select(classSession => classSession.Id), Is.SupersetOf(idsBefore));
            Assert.That(lastStart, Is.EqualTo(lastStartBefore.AddDays(7)));
            Assert.That(lastStart, Is.LessThanOrEqualTo(_clock.GetUtcNow() + FourWeeks));
        });
    }

    private ClassSessionSeeder Seeder()
    {
        ServiceProvider services = new ServiceCollection()
            .AddSingleton<IClassSessionRepository>(_classSessions)
            .AddSingleton<IReferenceDataWriter>(_referenceData)
            .BuildServiceProvider();
        return new ClassSessionSeeder(services.GetRequiredService<IServiceScopeFactory>(), _clock, NullLogger<ClassSessionSeeder>.Instance);
    }

    /// <summary>Class sessions kept in memory; a stored class session is the same object on every read.</summary>
    private sealed class StoredClassSessions : IClassSessionRepository
    {
        private readonly Dictionary<Guid, ClassSession> _stored = [];

        public List<ClassSession> All => [.. _stored.Values];

        public ClassSession? Get(Guid id)
        {
            return _stored.GetValueOrDefault(id);
        }

        public Task<IReadOnlyList<ClassSession>> ListAsync(Guid? centerId, DateTimeOffset startsAfter, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ClassSession>>([.. _stored.Values.Where(classSession => classSession.StartTime > startsAfter)]);
        }

        public Task<ClassSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Get(id));
        }

        public Task AddAsync(ClassSession classSession, CancellationToken cancellationToken)
        {
            // MongoDB refuses a second document with the same id, so a seeder that adds twice fails here too
            _stored.Add(classSession.Id, classSession);
            return Task.CompletedTask;
        }

        public Task<bool> SaveAsync(ClassSession classSession, CancellationToken cancellationToken)
        {
            _stored[classSession.Id] = classSession;
            return Task.FromResult(true);
        }
    }

    private sealed class StoredReferenceData : IReferenceDataWriter
    {
        public Dictionary<Guid, Center> Centers { get; } = [];

        public Dictionary<Guid, ClassType> ClassTypes { get; } = [];

        public Task SaveCenterAsync(Center center, CancellationToken cancellationToken)
        {
            Centers[center.Id] = center;
            return Task.CompletedTask;
        }

        public Task SaveClassTypeAsync(ClassType classType, CancellationToken cancellationToken)
        {
            ClassTypes[classType.Id] = classType;
            return Task.CompletedTask;
        }
    }
}
