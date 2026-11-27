using BookingService.Application;
using BookingService.Domain;
using BookingService.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace BookingService.Tests.Application;

/// <summary>A Træner or an Admin schedules a class session (#238).</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class ScheduleClassSessionCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Center Center = new(Guid.NewGuid(), "FitLife Center 1");
    private static readonly ClassType Yoga = new(Guid.NewGuid(), "Yoga");
    private static readonly ScheduleClassSessionCommand Valid = new(Center.Id, Yoga.Id, "Mette", Now.AddDays(1), DurationMinutes: 60, Capacity: 20);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Mock<IClassSessionRepository> _classSessions = new();
    private readonly Mock<IReferenceDataRepository> _referenceData = new();

    [SetUp]
    public void StoreTheReferenceData()
    {
        _referenceData.Setup(repository => repository.ListCentersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Center]);
        _referenceData.Setup(repository => repository.ListClassTypesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Yoga]);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task Handle_WithAStartThatIsNotInTheFuture_IsRejected(int minutesFromNow)
    {
        // Arrange
        ScheduleClassSessionCommand command = Valid with { StartsAt = Now.AddMinutes(minutesFromNow) };

        // Act
        ScheduleClassSessionResult result = await Handler().Handle(command, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((result, _classSessions.Invocations.Count), Is.EqualTo((Rejected(ScheduleClassSessionOutcome.StartNotInTheFuture), 0)));
    }

    [Test]
    public async Task Handle_WithACapacityUnderOne_IsRejected()
    {
        // Act
        ScheduleClassSessionResult result = await Handler().Handle(Valid with { Capacity = 0 }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((result, _classSessions.Invocations.Count), Is.EqualTo((Rejected(ScheduleClassSessionOutcome.CapacityUnderOne), 0)));
    }

    [Test]
    public async Task Handle_WithADurationUnderOneMinute_IsRejected()
    {
        // Act
        ScheduleClassSessionResult result = await Handler().Handle(Valid with { DurationMinutes = 0 }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((result, _classSessions.Invocations.Count), Is.EqualTo((Rejected(ScheduleClassSessionOutcome.DurationUnderOneMinute), 0)));
    }

    [TestCase("")]
    [TestCase("  ")]
    public async Task Handle_WithoutAnInstructorName_IsRejected(string instructorName)
    {
        // Act
        ScheduleClassSessionResult result = await Handler().Handle(Valid with { InstructorName = instructorName }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((result, _classSessions.Invocations.Count), Is.EqualTo((Rejected(ScheduleClassSessionOutcome.InstructorNameMissing), 0)));
    }

    [Test]
    public async Task Handle_WithAnUnknownCenter_IsNotFound()
    {
        // Act
        ScheduleClassSessionResult result = await Handler().Handle(Valid with { CenterId = Guid.NewGuid() }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((result, _classSessions.Invocations.Count), Is.EqualTo((Rejected(ScheduleClassSessionOutcome.CenterNotFound), 0)));
    }

    [Test]
    public async Task Handle_WithAnUnknownClassType_IsNotFound()
    {
        // Act
        ScheduleClassSessionResult result = await Handler().Handle(Valid with { ClassTypeId = Guid.NewGuid() }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((result, _classSessions.Invocations.Count), Is.EqualTo((Rejected(ScheduleClassSessionOutcome.ClassTypeNotFound), 0)));
    }

    [Test]
    public async Task Handle_WithValidData_SavesTheClassSessionOnce()
    {
        // Act
        await Handler().Handle(Valid, CancellationToken.None).ConfigureAwait(false);

        // Assert
        _classSessions.Verify(
            repository => repository.AddAsync(
                It.Is<ClassSession>(classSession =>
                    classSession.Details == new ClassSessionDetails(Center.Id, Yoga.Id, new Instructor("Mette"), TimeSpan.FromMinutes(60))
                    && classSession.StartTime == Now.AddDays(1)
                    && classSession.Capacity == new Capacity(20)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_WithValidData_ReturnsTheClassSessionAsTheClassScheduleShowsIt()
    {
        // Act
        ScheduleClassSessionResult result = await Handler().Handle(Valid, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Guid id = result.ClassSession?.Id ?? Guid.Empty;
        var expected = new ClassSessionSummary(
            id, Center.Id, Center.Name, Yoga.Id, Yoga.Name, "Mette", Now.AddDays(1), 60, 20, FreeSeats: 20, IsCancelled: false);
        Assert.That(
            (result.Outcome, result.ClassSession, id == Guid.Empty),
            Is.EqualTo((ScheduleClassSessionOutcome.Scheduled, expected, false)));
    }

    [Test]
    public async Task Handle_WithTheSmallestCapacityAndDuration_IsScheduled()
    {
        // Arrange
        ScheduleClassSessionCommand command = Valid with { Capacity = 1, DurationMinutes = 1 };

        // Act
        ScheduleClassSessionResult result = await Handler().Handle(command, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result.Outcome, Is.EqualTo(ScheduleClassSessionOutcome.Scheduled));
    }

    private static ScheduleClassSessionResult Rejected(ScheduleClassSessionOutcome outcome)
    {
        return new ScheduleClassSessionResult(outcome, ClassSession: null);
    }

    private ScheduleClassSessionCommandHandler Handler()
    {
        return new ScheduleClassSessionCommandHandler(_classSessions.Object, _referenceData.Object, _clock);
    }
}
