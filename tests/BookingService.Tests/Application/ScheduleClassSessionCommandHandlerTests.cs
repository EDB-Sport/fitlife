using BookingService.Application;
using BookingService.Domain;
using BookingService.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace BookingService.Tests.Application;

/// <summary>A Træner or an Admin schedules a class session (#238).</summary>
public sealed class ScheduleClassSessionCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Center Center = new(Guid.NewGuid(), "FitLife Center 1");
    private static readonly ClassType Yoga = new(Guid.NewGuid(), "Yoga");
    private static readonly ScheduleClassSessionCommand Valid = new(Center.Id, Yoga.Id, "Mette", Now.AddDays(1), DurationMinutes: 60, Capacity: 20);

    private FakeTimeProvider _clock = null!;
    private Mock<IClassSessionRepository> _classSessions = null!;
    private Mock<IReferenceDataRepository> _referenceData = null!;

    [SetUp]
    public void StoreTheReferenceData()
    {
        _clock = new FakeTimeProvider(Now);
        _classSessions = new Mock<IClassSessionRepository>();
        _referenceData = new Mock<IReferenceDataRepository>();
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
        AssertRejected(result, ScheduleClassSessionOutcome.StartNotInTheFuture);
    }

    [Test]
    public async Task Handle_WithACapacityUnderOne_IsRejected()
    {
        // Act
        ScheduleClassSessionResult result = await Handler().Handle(Valid with { Capacity = 0 }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        AssertRejected(result, ScheduleClassSessionOutcome.CapacityUnderOne);
    }

    [Test]
    public async Task Handle_WithADurationUnderOneMinute_IsRejected()
    {
        // Act
        ScheduleClassSessionResult result = await Handler().Handle(Valid with { DurationMinutes = 0 }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        AssertRejected(result, ScheduleClassSessionOutcome.DurationUnderOneMinute);
    }

    [TestCase("")]
    [TestCase("  ")]
    public async Task Handle_WithoutAnInstructorName_IsRejected(string instructorName)
    {
        // Act
        ScheduleClassSessionResult result = await Handler().Handle(Valid with { InstructorName = instructorName }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        AssertRejected(result, ScheduleClassSessionOutcome.InstructorNameMissing);
    }

    [Test]
    public async Task Handle_WithAnUnknownCenter_IsNotFound()
    {
        // Act
        ScheduleClassSessionResult result = await Handler().Handle(Valid with { CenterId = Guid.NewGuid() }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        AssertRejected(result, ScheduleClassSessionOutcome.CenterNotFound);
    }

    [Test]
    public async Task Handle_WithAnUnknownClassType_IsNotFound()
    {
        // Act
        ScheduleClassSessionResult result = await Handler().Handle(Valid with { ClassTypeId = Guid.NewGuid() }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        AssertRejected(result, ScheduleClassSessionOutcome.ClassTypeNotFound);
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
        ClassSessionSummary summary = result.ClassSession!;
        var expected = new ClassSessionSummary(
            summary.Id, Center.Id, Center.Name, Yoga.Id, Yoga.Name, "Mette", Now.AddDays(1), 60, 20, FreeSeats: 20, IsCancelled: false);
        Assert.That(
            (result.Outcome, summary, summary.Id == Guid.Empty),
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

    private void AssertRejected(ScheduleClassSessionResult result, ScheduleClassSessionOutcome outcome)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new ScheduleClassSessionResult(outcome, ClassSession: null)));
            _classSessions.Verify(repository => repository.AddAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    private ScheduleClassSessionCommandHandler Handler()
    {
        return new ScheduleClassSessionCommandHandler(_classSessions.Object, _referenceData.Object, _clock);
    }
}
