using BookingService.Application;
using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace BookingService.Tests.Application;

/// <summary>A member cancels their own reservation: Membership names the member, then the class session decides (#262).</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // fresh mocks for every test, so no call counts carry over
public sealed class CancelReservationCommandHandlerTests
{
    private const string BearerToken = "the-members-token";
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Mock<IMembershipClient> _membership = new();
    private readonly Mock<IClassSessionRepository> _classSessions = new();
    private readonly Guid _accountId = Guid.NewGuid();

    [SetUp]
    public void EverySaveSucceeds()
    {
        _classSessions.Setup(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    [Test]
    public async Task Handle_WhenTheMemberHasAReservation_CancelsItAndSavesOnce()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        MembershipAnswers(MemberLookup.Found(memberId, isActive: true));
        ClassSession classSession = Stored(reservedBy: memberId);

        // Act
        CancelReservationOutcome outcome = await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CancelReservationOutcome.Cancelled));
            Assert.That(classSession.Reservations, Is.Empty);
            _classSessions.Verify(repository => repository.SaveAsync(classSession, It.IsAny<CancellationToken>()), Times.Once);
            _membership.Verify(client => client.FindMemberAsync(_accountId, BearerToken, It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    [Test]
    public async Task Handle_WhenTheMembershipIsNotActive_StillCancels()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        MembershipAnswers(MemberLookup.Found(memberId, isActive: false));
        ClassSession classSession = Stored(reservedBy: memberId);

        // Act
        CancelReservationOutcome outcome = await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CancelReservationOutcome.Cancelled));
            Assert.That(classSession.Reservations, Is.Empty);
        });
    }

    [Test]
    public async Task Handle_WhenTheMemberHasNoReservationThere_SaysNotFoundAndSavesNothing()
    {
        // Arrange
        MembershipAnswers(MemberLookup.Found(Guid.NewGuid(), isActive: true));
        var otherMemberId = Guid.NewGuid();
        ClassSession classSession = Stored(reservedBy: otherMemberId);

        // Act
        CancelReservationOutcome outcome = await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CancelReservationOutcome.NoReservation));
            Assert.That(classSession.Reservations.Single().MemberId, Is.EqualTo(otherMemberId), "another member's reservation stays");
            _classSessions.Verify(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    [Test]
    public async Task Handle_WhenTheAccountIsNotAMember_SaysNotFoundAndNeverLoadsTheClassSession()
    {
        // Arrange
        MembershipAnswers(MemberLookup.NotAMember);

        // Act
        CancelReservationOutcome outcome = await Handler().Handle(Command(Guid.NewGuid()), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(CancelReservationOutcome.NoReservation));
        _classSessions.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenTheClassSessionDoesNotExist_SaysNotFound()
    {
        // Arrange
        MembershipAnswers(MemberLookup.Found(Guid.NewGuid(), isActive: true));

        // Act
        CancelReservationOutcome outcome = await Handler().Handle(Command(Guid.NewGuid()), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(CancelReservationOutcome.ClassSessionNotFound));
    }

    [Test]
    public async Task Handle_WhenMembershipIsUnavailable_SaysSoAndSavesNothing()
    {
        // Arrange
        MembershipAnswers(MemberLookup.Unavailable);

        // Act
        CancelReservationOutcome outcome = await Handler().Handle(Command(Guid.NewGuid()), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(CancelReservationOutcome.MembershipUnavailable));
        _classSessions.Verify(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenAnotherRequestSavedFirst_SaysItChangedMeanwhile()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        MembershipAnswers(MemberLookup.Found(memberId, isActive: true));
        ClassSession classSession = Stored(reservedBy: memberId);
        _classSessions.Setup(repository => repository.SaveAsync(classSession, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        CancelReservationOutcome outcome = await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(CancelReservationOutcome.ChangedMeanwhile));
    }

    private CancelReservationCommand Command(Guid classSessionId)
    {
        return new CancelReservationCommand(classSessionId, _accountId, BearerToken);
    }

    private void MembershipAnswers(MemberLookup answer)
    {
        _membership.Setup(client => client.FindMemberAsync(_accountId, BearerToken, It.IsAny<CancellationToken>())).ReturnsAsync(answer);
    }

    private ClassSession Stored(Guid reservedBy)
    {
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(1), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        classSession.Reserve(reservedBy, _clock);
        _classSessions.Setup(repository => repository.GetByIdAsync(classSession.Id, It.IsAny<CancellationToken>())).ReturnsAsync(classSession);
        return classSession;
    }

    private CancelReservationCommandHandler Handler()
    {
        return new CancelReservationCommandHandler(_membership.Object, _classSessions.Object, _clock);
    }
}
