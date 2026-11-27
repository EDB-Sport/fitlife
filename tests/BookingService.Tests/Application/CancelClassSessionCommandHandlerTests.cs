using BookingService.Application;
using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace BookingService.Tests.Application;

/// <summary>A Træner or an Admin cancels a class session (#239).</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // fresh mocks for every test, so no call counts carry over
public sealed class CancelClassSessionCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Mock<IClassSessionRepository> _classSessions = new();

    [SetUp]
    public void EverySaveSucceeds()
    {
        _classSessions.Setup(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    [Test]
    public async Task Handle_AnUnknownClassSession_IsNotFound()
    {
        // Act
        CancelClassSessionOutcome outcome = await Handler().Handle(new CancelClassSessionCommand(Guid.NewGuid()), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(CancelClassSessionOutcome.ClassSessionNotFound));
        _classSessions.Verify(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_AClassSessionWithReservations_CancelsItAndSavesOnce()
    {
        // Arrange
        ClassSession classSession = Stored();
        classSession.Reserve(Guid.NewGuid(), _clock);

        // Act
        CancelClassSessionOutcome outcome = await Handler().Handle(new CancelClassSessionCommand(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CancelClassSessionOutcome.Cancelled));
            Assert.That((classSession.IsCancelled, classSession.Reservations.Count), Is.EqualTo((true, 0)));
        });
        _classSessions.Verify(repository => repository.SaveAsync(classSession, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_AnAlreadyCancelledClassSession_IsRejectedAndNotSaved()
    {
        // Arrange
        ClassSession classSession = Stored();
        classSession.Cancel();
        int eventsBefore = classSession.DomainEvents.Count;

        // Act
        CancelClassSessionOutcome outcome = await Handler().Handle(new CancelClassSessionCommand(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CancelClassSessionOutcome.ClassSessionAlreadyCancelled));
            Assert.That(classSession.DomainEvents, Has.Count.EqualTo(eventsBefore));
        });
        _classSessions.Verify(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Handle_WhenAnotherRequestSavedFirst_LetsTheConflictThrough()
    {
        // Arrange
        ClassSession classSession = Stored();
        _classSessions.Setup(repository => repository.SaveAsync(classSession, It.IsAny<CancellationToken>())).ThrowsAsync(new ConcurrencyConflictException());

        // Act
        Task Act() => Handler().Handle(new CancelClassSessionCommand(classSession.Id), CancellationToken.None);

        // Assert
        Assert.ThrowsAsync<ConcurrencyConflictException>(Act);
    }

    private ClassSession Stored()
    {
        var classSession = new ClassSession(
            Guid.NewGuid(), new Capacity(2), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        _classSessions.Setup(repository => repository.GetByIdAsync(classSession.Id, It.IsAny<CancellationToken>())).ReturnsAsync(classSession);
        return classSession;
    }

    private CancelClassSessionCommandHandler Handler()
    {
        return new CancelClassSessionCommandHandler(_classSessions.Object);
    }
}
