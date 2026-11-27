using MembershipService.Application;
using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using MembershipService.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace MembershipService.Tests;

/// <summary>
/// Pausing a membership and ending the pause early (UC6, #248): the handlers find the membership through the token's
/// account, call the root with today's UTC date and save only a change.
/// </summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // fresh mocks for every test
public sealed class PauseHandlerTests
{
    private const string AccountId = "account-1";
    private static readonly DateOnly Today = new(2026, 11, 24);

    private readonly Mock<IMemberRepository> _members = new();
    private readonly Mock<IMembershipRepository> _memberships = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 24, 23, 30, 0, TimeSpan.Zero));
    private readonly Member _member = Member.Join(AccountId);
    private readonly Membership _membership;
    private readonly RecordingEventPublisher _events = new();

    public PauseHandlerTests()
    {
        _membership = Membership.Start(_member.Id, Today);
        _membership.MarkPaid();
        _members.Setup(members => members.FindByAccountIdAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(_member);
        _memberships.Setup(memberships => memberships.FindByMemberIdAsync(_member.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_membership);
    }

    private MembershipEventPublisher Events()
    {
        return new MembershipEventPublisher(_events, NullLogger<MembershipEventPublisher>.Instance);
    }

    [Test]
    public async Task HandleAsync_WhenPaused_SavesTheMembershipOnce()
    {
        // Arrange
        var handler = new PauseMembershipHandler(_members.Object, _memberships.Object, _clock, Events());

        // Act
        MembershipCommandResult<PauseResult> result = await handler.HandleAsync(
            new PauseMembershipCommand(AccountId, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo(PauseResult.Paused));
        Assert.That(result.Membership?.PausedFrom, Is.EqualTo(new DateOnly(2026, 12, 1)));
        _memberships.Verify(memberships => memberships.SaveAsync(_membership, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_WhenThePauseIsSaved_PublishesMembershipPaused()
    {
        // Arrange: #251, published after the save has returned
        bool savedFirst = false;
        _memberships.Setup(memberships => memberships.SaveAsync(_membership, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask)
            .Callback(() => _events.OnPublish = () => savedFirst = true);
        var handler = new PauseMembershipHandler(_members.Object, _memberships.Object, _clock, Events());

        // Act
        await handler.HandleAsync(new PauseMembershipCommand(AccountId, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_events.Published, Is.EqualTo(new object[] { new MembershipPaused(_member.Id.ToString(), new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)) }));
            Assert.That(savedFirst, Is.True);
        });
    }

    [Test]
    public async Task HandleAsync_WhenThePauseIsSaved_PublishesAnEventThatMatchesTheSchema()
    {
        // Arrange
        var handler = new PauseMembershipHandler(_members.Object, _memberships.Object, _clock, Events());
        await handler.HandleAsync(new PauseMembershipCommand(AccountId, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);
        var published = (MembershipPaused)_events.Published.Single();

        // Act
        IReadOnlyList<string> errors = EventSchemas.Validate(MembershipPaused.RoutingKey, Encoding.UTF8.GetString(EventJson.Serialize(published)));

        // Assert
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_WhenPublishingThrows_ThePauseIsStillSaved()
    {
        // Arrange: RabbitMQ can't be reached; the pause is kept and the member gets the same answer (#192's safe default)
        _events.Failure = new InvalidOperationException("RabbitMQ is down.");
        var handler = new PauseMembershipHandler(_members.Object, _memberships.Object, _clock, Events());

        // Act
        MembershipCommandResult<PauseResult> result = await handler.HandleAsync(
            new PauseMembershipCommand(AccountId, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo(PauseResult.Paused));
        _memberships.Verify(memberships => memberships.SaveAsync(_membership, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_WhenThePauseIsRejected_PublishesNothing()
    {
        // Arrange
        var handler = new PauseMembershipHandler(_members.Object, _memberships.Object, _clock, Events());

        // Act
        await handler.HandleAsync(new PauseMembershipCommand(AccountId, Today.AddDays(-1), new DateOnly(2026, 12, 31)), CancellationToken.None);

        // Assert
        Assert.That(_events.Published, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_WhenThePauseIsRejected_SavesNothing()
    {
        // Arrange
        var handler = new PauseMembershipHandler(_members.Object, _memberships.Object, _clock, Events());

        // Act
        MembershipCommandResult<PauseResult> result = await handler.HandleAsync(
            new PauseMembershipCommand(AccountId, Today.AddDays(-1), new DateOnly(2026, 12, 31)), CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo(PauseResult.PauseStartsInThePast));
        _memberships.Verify(memberships => memberships.SaveAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_TodayIsTheUtcDate_APauseFromTodayIsAccepted()
    {
        // Arrange: 23:30 UTC on 24 Nov is already 25 Nov in Denmark, but "today" is the UTC date
        var handler = new PauseMembershipHandler(_members.Object, _memberships.Object, _clock, Events());

        // Act
        MembershipCommandResult<PauseResult> result = await handler.HandleAsync(
            new PauseMembershipCommand(AccountId, Today, new DateOnly(2026, 12, 31)), CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo(PauseResult.Paused));
    }

    [Test]
    public async Task HandleAsync_WhenTheAccountHasNotJoined_IsNotAMember()
    {
        // Arrange
        var handler = new PauseMembershipHandler(_members.Object, _memberships.Object, _clock, Events());

        // Act
        MembershipCommandResult<PauseResult> result = await handler.HandleAsync(
            new PauseMembershipCommand("another-account", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);

        // Assert
        Assert.That(result.IsMember, Is.False);
        _memberships.Verify(memberships => memberships.SaveAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EndPause_WhenThePauseIsEnded_SavesTheMembershipOnce()
    {
        // Arrange
        _membership.Pause(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Today);
        var handler = new EndPauseHandler(_members.Object, _memberships.Object, _clock);

        // Act
        MembershipCommandResult<EndPauseResult> result = await handler.HandleAsync(new EndPauseCommand(AccountId), CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo(EndPauseResult.PauseEnded));
        _memberships.Verify(memberships => memberships.SaveAsync(_membership, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task EndPause_WithNoPause_SavesNothing()
    {
        // Arrange
        var handler = new EndPauseHandler(_members.Object, _memberships.Object, _clock);

        // Act
        MembershipCommandResult<EndPauseResult> result = await handler.HandleAsync(new EndPauseCommand(AccountId), CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo(EndPauseResult.NoPauseToEnd));
        _memberships.Verify(memberships => memberships.SaveAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EndPause_WhenTheAccountHasNotJoined_IsNotAMember()
    {
        // Arrange
        var handler = new EndPauseHandler(_members.Object, _memberships.Object, _clock);

        // Act
        MembershipCommandResult<EndPauseResult> result = await handler.HandleAsync(new EndPauseCommand("another-account"), CancellationToken.None);

        // Assert
        Assert.That(result.IsMember, Is.False);
    }
}
