using MembershipService.Application;
using MembershipService.Domain;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace MembershipService.Tests;

/// <summary>Joining FitLife: one member per account, with an active membership (#170).</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // fresh in-memory stores for every test
public sealed class JoinMembershipHandlerTests
{
    private readonly InMemoryMembers _members = new();
    private readonly InMemoryMemberships _memberships = new();
    private readonly StubPaymentProvider _payments = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 24, 9, 0, 0, TimeSpan.Zero));

    private JoinMembershipHandler Handler()
    {
        return new JoinMembershipHandler(_members, _memberships, _payments, Options.Create(new MembershipFeeSettings { MonthlyFeeDkk = 299m }), _clock);
    }

    [Test]
    public async Task Join_NewAccount_CreatesAMemberWithAnActiveMembership()
    {
        // Arrange
        JoinMembershipHandler handler = Handler();

        // Act
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(result.Joined, Is.True);
        Assert.That(_members.Stored.Single().AccountId, Is.EqualTo("account-1"));
        Assert.That(_memberships.Stored.Single().MemberId, Is.EqualTo(result.Member?.Id));
        Assert.That(result.Membership?.IsActive, Is.True);
    }

    [Test]
    public async Task Join_NewAccount_StartsTheMembershipToday()
    {
        // Arrange
        JoinMembershipHandler handler = Handler();

        // Act
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(result.Membership?.StartDate, Is.EqualTo(new DateOnly(2026, 11, 24)));
    }

    [Test]
    public async Task Join_WhenAccountIsAlreadyAMember_IsRejected()
    {
        // Arrange
        JoinMembershipHandler handler = Handler();
        await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Act
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(result.Joined, Is.False);
        Assert.That(_members.Stored, Has.Count.EqualTo(1));
        Assert.That(_memberships.Stored, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Join_WhenAnotherJoinForTheAccountWinsTheRace_IsRejectedAndLeavesNoMembership()
    {
        // Arrange: the lookup misses the other join, the store's unique account id catches it
        _members.RejectNextAdd = true;
        JoinMembershipHandler handler = Handler();

        // Act
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(result.Joined, Is.False);
        Assert.That(_memberships.Stored, Is.Empty);
    }

    [Test]
    public async Task Join_WhenTheFirstFeeIsDeclined_TheMembershipHasPaymentMissing()
    {
        // Arrange
        _payments.Paid = false;
        JoinMembershipHandler handler = Handler();

        // Act
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(result.Membership?.Status, Is.EqualTo(MembershipStatus.PaymentMissing));
        Assert.That(_memberships.Stored.Single().Status, Is.EqualTo(MembershipStatus.PaymentMissing));
    }

    [Test]
    public async Task Join_NewAccount_ChargesTheMonthlyFeeForTheNewMember()
    {
        // Arrange
        JoinMembershipHandler handler = Handler();

        // Act
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(_payments.Charges, Is.EqualTo(new[] { (result.Member?.Id ?? Guid.Empty, 299m) }));
    }

    [Test]
    public async Task Join_WhenAccountIsAlreadyAMember_ChargesNothing()
    {
        // Arrange
        JoinMembershipHandler handler = Handler();
        await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Act
        await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(_payments.Charges, Has.Count.EqualTo(1));
    }

    private sealed class StubPaymentProvider : IPaymentProvider
    {
        public bool Paid { get; set; } = true;

        public List<(Guid MemberId, decimal AmountDkk)> Charges { get; } = [];

        public Task<bool> ChargeMembershipFeeAsync(Guid memberId, decimal amountDkk, CancellationToken cancellationToken)
        {
            Charges.Add((memberId, amountDkk));
            return Task.FromResult(Paid);
        }
    }

    private sealed class InMemoryMembers : IMemberRepository
    {
        public List<Member> Stored { get; } = [];

        public bool RejectNextAdd { get; set; }

        public Task<Member?> FindByAccountIdAsync(string accountId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Stored.Find(member => string.Equals(member.AccountId, accountId, StringComparison.Ordinal)));
        }

        public Task<bool> TryAddAsync(Member member, CancellationToken cancellationToken)
        {
            if (RejectNextAdd || Stored.Exists(stored => string.Equals(stored.AccountId, member.AccountId, StringComparison.Ordinal)))
            {
                RejectNextAdd = false;
                return Task.FromResult(false);
            }

            Stored.Add(member);
            return Task.FromResult(true);
        }
    }

    [Test]
    public async Task Join_WhenTheFirstFeeIsDeclined_TheMembershipHasPaymentMissing()
    {
        // Arrange
        _payments.Paid = false;
        JoinMembershipHandler handler = Handler();

        // Act
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(result.Membership?.Status, Is.EqualTo(MembershipStatus.PaymentMissing));
        Assert.That(_memberships.Stored.Single().Status, Is.EqualTo(MembershipStatus.PaymentMissing));
    }

    [Test]
    public async Task Join_NewAccount_ChargesTheMonthlyFeeForTheNewMember()
    {
        // Arrange
        JoinMembershipHandler handler = Handler();

        // Act
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(_payments.Charges, Is.EqualTo(new[] { (result.Member?.Id ?? Guid.Empty, 299m) }));
    }

    [Test]
    public async Task Join_WhenAccountIsAlreadyAMember_ChargesNothing()
    {
        // Arrange
        JoinMembershipHandler handler = Handler();
        await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Act
        await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(_payments.Charges, Has.Count.EqualTo(1));
    }

    private sealed class StubPaymentProvider : IPaymentProvider
    {
        public bool Paid { get; set; } = true;

        public List<(Guid MemberId, decimal AmountDkk)> Charges { get; } = [];

        public Task<bool> ChargeMembershipFeeAsync(Guid memberId, decimal amountDkk, CancellationToken cancellationToken)
        {
            Charges.Add((memberId, amountDkk));
            return Task.FromResult(Paid);
        }
    }

    private sealed class InMemoryMemberships : IMembershipRepository
    {
        public List<Membership> Stored { get; } = [];

        public Task AddAsync(Membership membership, CancellationToken cancellationToken)
        {
            Stored.Add(membership);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid membershipId, CancellationToken cancellationToken)
        {
            Stored.RemoveAll(membership => membership.Id == membershipId);
            return Task.CompletedTask;
        }

        public Task SaveAsync(Membership membership, CancellationToken cancellationToken)
        {
            Stored.RemoveAll(stored => stored.Id == membership.Id);
            Stored.Add(membership);
            return Task.CompletedTask;
        }

        public Task<Membership?> FindByMemberIdAsync(Guid memberId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Stored.Find(membership => membership.MemberId == memberId));
        }
    }
}
