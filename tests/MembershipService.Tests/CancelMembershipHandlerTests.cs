using MembershipService.Application;
using MembershipService.Domain;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace MembershipService.Tests;

/// <summary>
/// Cancelling a membership (UC8, #249): the handler finds the membership through the token's account, calls the root with
/// today's UTC date and saves only a change.
/// </summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // fresh mocks for every test
public sealed class CancelMembershipHandlerTests
{
    private const string AccountId = "account-1";
    private static readonly DateOnly Today = new(2026, 11, 24);

    private readonly Mock<IMemberRepository> _members = new();
    private readonly Mock<IMembershipRepository> _memberships = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 30, 23, 30, 0, TimeSpan.Zero));
    private readonly Member _member = Member.Join(AccountId);
    private readonly Membership _membership;

    public CancelMembershipHandlerTests()
    {
        _membership = Membership.Start(_member.Id, Today);
        _membership.MarkPaid();
        _members.Setup(members => members.FindByAccountIdAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(_member);
        _memberships.Setup(memberships => memberships.FindByMemberIdAsync(_member.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_membership);
    }

    [Test]
    public async Task HandleAsync_WhenCancelled_SavesTheMembershipOnce()
    {
        // Arrange: 23:30 UTC on 30 Nov is already 1 Dec in Denmark, but "today" is the UTC date
        var handler = new CancelMembershipHandler(_members.Object, _memberships.Object, _clock);

        // Act
        MembershipCommandResult<CancelResult> result = await handler.HandleAsync(new CancelMembershipCommand(AccountId), CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo(CancelResult.Cancelled));
        Assert.That(result.Membership?.LastTrainingDay, Is.EqualTo(new DateOnly(2026, 12, 31)));
        _memberships.Verify(memberships => memberships.SaveAsync(_membership, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_WhenAlreadyCancelled_SavesNothing()
    {
        // Arrange
        _membership.Cancel(Today);
        var handler = new CancelMembershipHandler(_members.Object, _memberships.Object, _clock);

        // Act
        MembershipCommandResult<CancelResult> result = await handler.HandleAsync(new CancelMembershipCommand(AccountId), CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.EqualTo(CancelResult.MembershipAlreadyCancelled));
        _memberships.Verify(memberships => memberships.SaveAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_WhenTheAccountHasNotJoined_IsNotAMember()
    {
        // Arrange
        var handler = new CancelMembershipHandler(_members.Object, _memberships.Object, _clock);

        // Act
        MembershipCommandResult<CancelResult> result = await handler.HandleAsync(new CancelMembershipCommand("another-account"), CancellationToken.None);

        // Assert
        Assert.That(result.IsMember, Is.False);
        _memberships.Verify(memberships => memberships.SaveAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
