using BookingService.Application;
using BookingService.Domain;
using BookingService.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace BookingService.Tests.Application;

/// <summary>The class schedule: upcoming class sessions with their free seats and the names of their centre and class type (#254).</summary>
public sealed class GetClassSessionsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly Center Center = new(Guid.NewGuid(), "FitLife Center 1");
    private static readonly ClassType Yoga = new(Guid.NewGuid(), "Yoga");

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Mock<IClassSessionRepository> _classSessions = new();
    private readonly Mock<IReferenceDataRepository> _referenceData = new();

    [SetUp]
    public void StoreTheReferenceData()
    {
        _referenceData.Setup(repository => repository.ListCentersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Center]);
        _referenceData.Setup(repository => repository.ListClassTypesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Yoga]);
    }

    [Test]
    public async Task Handle_WithReservations_ReturnsCapacityMinusReservationsAsFreeSeats()
    {
        // Arrange
        ClassSession classSession = ClassSessionIn(Center.Id, capacity: 3);
        classSession.Reserve(Guid.NewGuid(), _clock);
        Stored(classSession);

        // Act
        IReadOnlyList<ClassSessionSummary> schedule = await Handler().Handle(new GetClassSessionsQuery(CenterId: null), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(schedule.Single().FreeSeats, Is.EqualTo(2));
    }

    [Test]
    public async Task Handle_AsksForClassSessionsThatStartAfterNow()
    {
        // Arrange
        Stored();
        var centerId = Guid.NewGuid();

        // Act
        await Handler().Handle(new GetClassSessionsQuery(centerId), CancellationToken.None).ConfigureAwait(false);

        // Assert
        _classSessions.Verify(repository => repository.ListAsync(centerId, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ReturnsTheNamesAndDetails()
    {
        // Arrange
        ClassSession classSession = ClassSessionIn(Center.Id, capacity: 3);
        Stored(classSession);

        // Act
        IReadOnlyList<ClassSessionSummary> schedule = await Handler().Handle(new GetClassSessionsQuery(CenterId: null), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(schedule, Is.EqualTo(new[]
        {
            new ClassSessionSummary(classSession.Id, Center.Id, "FitLife Center 1", Yoga.Id, "Yoga", "Mette", classSession.StartTime, DurationMinutes: 45, Capacity: 3, FreeSeats: 3, IsCancelled: false),
        }));
    }

    [Test]
    public async Task Handle_WhenTheCentreIsNotInTheReferenceData_ReturnsAnEmptyName()
    {
        // Arrange
        Stored(ClassSessionIn(Guid.NewGuid(), capacity: 3));

        // Act
        IReadOnlyList<ClassSessionSummary> schedule = await Handler().Handle(new GetClassSessionsQuery(CenterId: null), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(schedule.Single().CenterName, Is.Empty);
    }

    private static ClassSession ClassSessionIn(Guid centerId, int capacity)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(capacity), Now.AddDays(1), new ClassSessionDetails(centerId, Yoga.Id, new Instructor("Mette"), TimeSpan.FromMinutes(45)));
    }

    private void Stored(params ClassSession[] classSessions)
    {
        _classSessions.Setup(repository => repository.ListAsync(It.IsAny<Guid?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(classSessions);
    }

    private GetClassSessionsQueryHandler Handler()
    {
        return new GetClassSessionsQueryHandler(_classSessions.Object, _referenceData.Object, _clock);
    }
}
