using BookingService.Application;
using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace BookingService.Tests.Application;

/// <summary>A member reserves a seat: Membership first, then the class session decides (#260).</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // fresh mocks for every test, so no call counts carry over
public sealed class ReserveSeatCommandHandlerTests
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
    public async Task Handle_WhenNotAMember_IsRejectedAndNeverLoadsTheClassSession()
    {
        // Arrange
        MembershipAnswers(MemberLookup.NotAMember);

        // Act
        ReserveSeatResult result = await Handler().Handle(Command(Guid.NewGuid()), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result.Outcome, Is.EqualTo(ReserveSeatOutcome.MembershipNotActive));
        _classSessions.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenTheMembershipIsNotActive_IsRejectedAndNeverLoadsTheClassSession()
    {
        // Arrange
        MembershipAnswers(MemberLookup.Found(Guid.NewGuid(), isActive: false));

        // Act
        ReserveSeatResult result = await Handler().Handle(Command(Guid.NewGuid()), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result.Outcome, Is.EqualTo(ReserveSeatOutcome.MembershipNotActive));
        _classSessions.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenMembershipIsUnavailable_SaysSoAndSavesNothing()
    {
        // Arrange
        MembershipAnswers(MemberLookup.Unavailable);

        // Act
        ReserveSeatResult result = await Handler().Handle(Command(Guid.NewGuid()), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result.Outcome, Is.EqualTo(ReserveSeatOutcome.MembershipUnavailable));
        _classSessions.Verify(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenTheClassSessionIsUnknown_SaysNotFound()
    {
        // Arrange
        MembershipAnswers(MemberLookup.Found(Guid.NewGuid(), isActive: true));

        // Act
        ReserveSeatResult result = await Handler().Handle(Command(Guid.NewGuid()), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result.Outcome, Is.EqualTo(ReserveSeatOutcome.ClassSessionNotFound));
    }

    [Test]
    public async Task Handle_WithAnActiveMembership_ReservesForTheMemberIdAndSavesOnce()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        MembershipAnswers(MemberLookup.Found(memberId, isActive: true));
        ClassSession classSession = Stored(capacity: 1);

        // Act
        ReserveSeatResult result = await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new ReserveSeatResult(ReserveSeatOutcome.Answered, ReservationResult.Made, memberId)));
            Assert.That(classSession.Reservations.Single().MemberId, Is.EqualTo(memberId), "the reservation stores the member id, not the account id");
            _classSessions.Verify(repository => repository.SaveAsync(classSession, It.IsAny<CancellationToken>()), Times.Once);
            _membership.Verify(client => client.FindMemberAsync(_accountId, BearerToken, It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    [Test]
    public async Task Handle_WhenTheClassSessionIsFull_PassesTheResultOnAndDoesNotSave()
    {
        // Arrange
        MembershipAnswers(MemberLookup.Found(Guid.NewGuid(), isActive: true));
        ClassSession classSession = Stored(capacity: 1);
        classSession.Reserve(Guid.NewGuid(), _clock);

        // Act
        ReserveSeatResult result = await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((result.Outcome, result.Reservation), Is.EqualTo((ReserveSeatOutcome.Answered, ReservationResult.ClassSessionFull)));
        _classSessions.Verify(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenAnotherRequestSavedFirst_SaysItChangedMeanwhile()
    {
        // Arrange
        MembershipAnswers(MemberLookup.Found(Guid.NewGuid(), isActive: true));
        ClassSession classSession = Stored(capacity: 1);
        _classSessions.Setup(repository => repository.SaveAsync(classSession, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        ReserveSeatResult result = await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result.Outcome, Is.EqualTo(ReserveSeatOutcome.ChangedMeanwhile));
    }

    private ReserveSeatCommand Command(Guid classSessionId)
    {
        return new ReserveSeatCommand(classSessionId, _accountId, BearerToken);
    }

    private void MembershipAnswers(MemberLookup answer)
    {
        _membership.Setup(client => client.FindMemberAsync(_accountId, BearerToken, It.IsAny<CancellationToken>())).ReturnsAsync(answer);
    }

    private ClassSession Stored(int capacity)
    {
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(capacity), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        _classSessions.Setup(repository => repository.GetByIdAsync(classSession.Id, It.IsAny<CancellationToken>())).ReturnsAsync(classSession);
        return classSession;
    }

    private ReserveSeatCommandHandler Handler()
    {
        return new ReserveSeatCommandHandler(_membership.Object, _classSessions.Object, _clock);
    }
}
