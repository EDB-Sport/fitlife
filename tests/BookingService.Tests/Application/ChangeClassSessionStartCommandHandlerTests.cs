using BookingService.Application;
using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace BookingService.Tests.Application;

/// <summary>A Træner or an Admin moves a class session to a new start time (#241).</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // fresh mocks for every test, so no call counts carry over
public sealed class ChangeClassSessionStartCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Mock<IClassSessionRepository> _classSessions = new();

    [SetUp]
    public void EverySaveSucceeds()
    {
        _classSessions.Setup(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    [Test]
    public async Task Handle_AnUnknownClassSession_IsNotFound()
    {
        // Act
        ChangeClassSessionStartOutcome outcome = await Handler().Handle(new ChangeClassSessionStartCommand(Guid.NewGuid(), Now.AddDays(2)), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(ChangeClassSessionStartOutcome.ClassSessionNotFound));
        _classSessions.Verify(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenChanged_SavesOnce()
    {
        // Arrange
        ClassSession classSession = Stored();
        classSession.Reserve(Guid.NewGuid(), _clock);
        DateTimeOffset newStart = Now.AddDays(2);

        // Act
        ChangeClassSessionStartOutcome outcome = await Handler().Handle(new ChangeClassSessionStartCommand(classSession.Id, newStart), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(ChangeClassSessionStartOutcome.Changed));
            Assert.That((classSession.StartTime, classSession.Reservations.Count), Is.EqualTo((newStart, 1)));
        });
        _classSessions.Verify(repository => repository.SaveAsync(classSession, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(ChangeClassSessionStartOutcome.ClassSessionCancelled)]
    [TestCase(ChangeClassSessionStartOutcome.StartNotInTheFuture)]
    [TestCase(ChangeClassSessionStartOutcome.ClassSessionStarted)]
    public async Task Handle_WhenTheAggregateRejects_PassesTheResultOnAndSavesNothing(ChangeClassSessionStartOutcome expected)
    {
        // Arrange
        ClassSession classSession = Stored();
        DateTimeOffset newStart = Now.AddDays(2);
        switch (expected)
        {
            case ChangeClassSessionStartOutcome.ClassSessionCancelled:
                classSession.Cancel(_clock);
                break;
            case ChangeClassSessionStartOutcome.StartNotInTheFuture:
                newStart = Now;
                break;
            default:
                _clock.SetUtcNow(classSession.StartTime);
                break;
        }

        DateTimeOffset startBefore = classSession.StartTime;

        // Act
        ChangeClassSessionStartOutcome outcome = await Handler().Handle(new ChangeClassSessionStartCommand(classSession.Id, newStart), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(expected));
            Assert.That(classSession.StartTime, Is.EqualTo(startBefore));
        });
        _classSessions.Verify(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenAnotherRequestSavedFirst_SaysSo()
    {
        // Arrange
        ClassSession classSession = Stored();
        _classSessions.Setup(repository => repository.SaveAsync(classSession, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        ChangeClassSessionStartOutcome outcome = await Handler().Handle(new ChangeClassSessionStartCommand(classSession.Id, Now.AddDays(2)), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(ChangeClassSessionStartOutcome.ChangedMeanwhile));
    }

    private ClassSession Stored()
    {
        var classSession = new ClassSession(
            Guid.NewGuid(), new Capacity(2), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        _classSessions.Setup(repository => repository.GetByIdAsync(classSession.Id, It.IsAny<CancellationToken>())).ReturnsAsync(classSession);
        return classSession;
    }

    private ChangeClassSessionStartCommandHandler Handler()
    {
        return new ChangeClassSessionStartCommandHandler(_classSessions.Object, _clock);
    }
}
