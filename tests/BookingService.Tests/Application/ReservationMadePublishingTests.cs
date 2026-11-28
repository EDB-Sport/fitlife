using System.Text;
using BookingService.Application;
using BookingService.Domain;
using FitLife.Contracts.Testing;
using FitLife.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using ReservationMadeEvent = FitLife.Messaging.Events.ReservationMade;

namespace BookingService.Tests.Application;

/// <summary>A reserved seat is published as <c>ReservationMade</c> after it is stored, and only then (#192).</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // fresh fakes for every test, so nothing published carries over
public sealed class ReservationMadePublishingTests
{
    private const string BearerToken = "the-members-token";
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Mock<IMembershipClient> _membership = new();
    private readonly Mock<IClassSessionRepository> _classSessions = new();
    private readonly RecordingEventPublisher _events = new();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _memberId = Guid.NewGuid();
    private bool _saved;

    [SetUp]
    public void AnActiveMemberWhoseSavesSucceed()
    {
        _membership.Setup(client => client.FindMemberAsync(_accountId, BearerToken, It.IsAny<CancellationToken>())).ReturnsAsync(MemberLookup.Found(_memberId, isActive: true));
        _classSessions.Setup(repository => repository.SaveAsync(It.IsAny<ClassSession>(), It.IsAny<CancellationToken>()))
            .Callback(() => _saved = true)
            .ReturnsAsync(true);
    }

    [Test]
    public async Task Handle_WhenTheSeatIsReserved_PublishesReservationMadeAfterTheSave()
    {
        // Arrange
        ClassSession classSession = Stored(capacity: 1);
        bool savedBeforePublish = false;
        _events.OnPublish = () => savedBeforePublish = _saved;

        // Act
        await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Reservation reservation = classSession.Reservations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(
                _events.Published,
                Is.EqualTo(new object[] { new ReservationMadeEvent(reservation.Id.ToString(), classSession.Id.ToString(), _memberId.ToString(), classSession.StartTime) }));
            Assert.That(savedBeforePublish, Is.True);
        });
    }

    [Test]
    public async Task Handle_WhenTheClassSessionIsFull_PublishesNothing()
    {
        // Arrange
        ClassSession classSession = Stored(capacity: 1);
        classSession.Reserve(Guid.NewGuid(), _clock);

        // Act
        await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(_events.Published, Is.Empty);
    }

    [Test]
    public async Task Handle_WhenAnotherRequestSavedFirst_PublishesNothing()
    {
        // Arrange
        ClassSession classSession = Stored(capacity: 1);
        _classSessions.Setup(repository => repository.SaveAsync(classSession, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(_events.Published, Is.Empty);
    }

    [Test]
    public async Task Handle_WhenThePublishFails_KeepsTheReservationAndAnswersMade()
    {
        // Arrange: RabbitMQ is down; there is no outbox, so the event is lost (the issue's safe default)
        ClassSession classSession = Stored(capacity: 1);
        _events.Failure = new InvalidOperationException("RabbitMQ is down.");

        // Act
        ReserveSeatResult result = await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((result.Outcome, result.Reservation, _saved), Is.EqualTo((ReserveSeatOutcome.Answered, ReservationResult.Made, true)));
    }

    [Test]
    public async Task Handle_WhenTheSeatIsReserved_PublishesAnEventThatMatchesTheSchema()
    {
        // Arrange
        ClassSession classSession = Stored(capacity: 1);

        // Act
        await Handler().Handle(Command(classSession.Id), CancellationToken.None).ConfigureAwait(false);

        // Assert
        var published = (ReservationMadeEvent)_events.Published.Single();
        IReadOnlyList<string> errors = EventSchemas.Validate(ReservationMadeEvent.RoutingKey, Encoding.UTF8.GetString(EventJson.Serialize(published)));
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task PublishReservationsMade_AfterAHandOverFromTheWaitlist_PublishesTheSeatOfTheFirstWaitlistEntry()
    {
        // Arrange
        ClassSession classSession = Stored(capacity: 1);
        var leavingMemberId = Guid.NewGuid();
        var waitingMemberId = Guid.NewGuid();
        classSession.Reserve(leavingMemberId, _clock);
        classSession.JoinWaitlist(waitingMemberId, _clock);
        classSession.CancelReservation(leavingMemberId, _clock);

        // Act
        await Publisher().PublishReservationsMadeAsync(classSession).ConfigureAwait(false);

        // Assert
        Reservation fromWaitlist = classSession.Reservations.Single();
        Assert.That(
            _events.Published,
            Is.EqualTo(new object[] { new ReservationMadeEvent(fromWaitlist.Id.ToString(), classSession.Id.ToString(), waitingMemberId.ToString(), classSession.StartTime) }));
    }

    private ReserveSeatCommand Command(Guid classSessionId)
    {
        return new ReserveSeatCommand(classSessionId, _accountId, BearerToken);
    }

    private ClassSession Stored(int capacity)
    {
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(capacity), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        _classSessions.Setup(repository => repository.GetByIdAsync(classSession.Id, It.IsAny<CancellationToken>())).ReturnsAsync(classSession);
        return classSession;
    }

    private BookingEventPublisher Publisher()
    {
        return new BookingEventPublisher(_events, NullLogger<BookingEventPublisher>.Instance);
    }

    private ReserveSeatCommandHandler Handler()
    {
        return new ReserveSeatCommandHandler(_membership.Object, _classSessions.Object, Publisher(), _clock);
    }
}
