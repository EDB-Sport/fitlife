using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using MembershipService.Application;
using MembershipService.Domain;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly RecordingEventPublisher _events = new();

    private JoinMembershipHandler Handler()
    {
        return new JoinMembershipHandler(
            _members,
            _memberships,
            new MembershipFee(_payments, Options.Create(new MembershipFeeSettings { MonthlyFeeDkk = 350m })),
            _clock,
            new MembershipEventPublisher(_events, NullLogger<MembershipEventPublisher>.Instance));
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
    public async Task Join_WhenTheFirstFeeIsPaid_StoresAnActiveMembership()
    {
        // Arrange
        JoinMembershipHandler handler = Handler();

        // Act
        await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(_memberships.Stored.Single().Status, Is.EqualTo(MembershipStatus.Active));
    }

    [Test]
    public async Task Join_WhenTheChargeIsInterrupted_LeavesNoActiveMembership()
    {
        // Arrange: the request is cancelled while the provider is charging, as when the client goes away
        _payments.Interrupt = true;
        JoinMembershipHandler handler = Handler();

        // Act
        Func<Task> act = () => handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        await Assert.ThatAsync(act, Throws.InstanceOf<OperationCanceledException>()).ConfigureAwait(false);
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
        Assert.That(_payments.Charges, Is.EqualTo(new[] { (result.Member?.Id ?? Guid.Empty, 350m) }));
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

    [Test]
    public async Task HandleAsync_WhenTheRequestIsCancelledDuringTheCharge_StillSavesTheMembershipAsActive()
    {
        // Arrange: the caller goes away after the provider has taken the charge (#544)
        using var request = new CancellationTokenSource();
        _payments.DuringCharge = request.Cancel;
        JoinMembershipHandler handler = Handler();

        // Act
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand("account-1"), request.Token);

        // Assert
        Assert.That(result.Joined, Is.True);
        Assert.That(_memberships.Stored.Single().Status, Is.EqualTo(MembershipStatus.Active));
    }

    [Test]
    public async Task HandleAsync_WhenTheRequestIsCancelledBeforeTheCharge_ChargesNothing()
    {
        // Arrange
        using var request = new CancellationTokenSource();
        await request.CancelAsync().ConfigureAwait(false);
        JoinMembershipHandler handler = Handler();

        // Act
        Func<Task> act = () => handler.HandleAsync(new JoinMembershipCommand("account-1"), request.Token);

        // Assert
        await Assert.ThatAsync(act, Throws.InstanceOf<OperationCanceledException>()).ConfigureAwait(false);
        Assert.That(_payments.Charges, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_WhenTheChargeFails_PublishesPaymentFailed()
    {
        // Arrange: #251, after the membership is stored with Betaling mangler
        _payments.Paid = false;
        JoinMembershipHandler handler = Handler();

        // Act
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(_events.Published, Is.EqualTo(new object[] { new PaymentFailed(result.Member?.Id.ToString() ?? "", _clock.GetUtcNow()) }));
    }

    [Test]
    public async Task HandleAsync_WhenTheChargeFails_PublishesAnEventThatMatchesTheSchema()
    {
        // Arrange
        _payments.Paid = false;
        await Handler().HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);
        var published = (PaymentFailed)_events.Published.Single();

        // Act
        IReadOnlyList<string> errors = EventSchemas.Validate(PaymentFailed.RoutingKey, Encoding.UTF8.GetString(EventJson.Serialize(published)));

        // Assert
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_WhenTheChargeIsAccepted_PublishesNothing()
    {
        // Act
        await Handler().HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.That(_events.Published, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_WhenPublishingThrows_TheMembershipIsStillStored()
    {
        // Arrange: RabbitMQ can't be reached; the join is kept and the member gets the same answer (#192's safe default)
        _payments.Paid = false;
        _events.Failure = new InvalidOperationException("RabbitMQ is down.");

        // Act
        JoinResult result = await Handler().HandleAsync(new JoinMembershipCommand("account-1"), CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Joined, Is.True);
            Assert.That(_memberships.Stored.Single().Status, Is.EqualTo(MembershipStatus.PaymentMissing));
        });
    }

    private sealed class StubPaymentProvider : IPaymentProvider
    {
        public bool Paid { get; set; } = true;

        /// <summary>Runs while the charge is being made, before the provider answers.</summary>
        public Action? DuringCharge { get; set; }

        public bool Interrupt { get; set; }

        public List<(Guid MemberId, decimal AmountDkk)> Charges { get; } = [];

        public Task<bool> ChargeMembershipFeeAsync(Guid memberId, decimal amountDkk, CancellationToken cancellationToken)
        {
            Charges.Add((memberId, amountDkk));
            if (Interrupt)
            {
                throw new OperationCanceledException("The request was cancelled while the fee was being charged.");
            }

            DuringCharge?.Invoke();
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

    private sealed class InMemoryMemberships : IMembershipRepository
    {
        public List<Membership> Stored { get; } = [];

        // Copies, as a database does: a change to a membership is stored only when it is saved
        private static Membership Copy(Membership membership)
        {
            return Membership.Restore(membership.Id, membership.MemberId, membership.Type, membership.Status, membership.StartDate);
        }

        public Task AddAsync(Membership membership, CancellationToken cancellationToken)
        {
            Stored.Add(Copy(membership));
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid membershipId, CancellationToken cancellationToken)
        {
            Stored.RemoveAll(membership => membership.Id == membershipId);
            return Task.CompletedTask;
        }

        public Task SaveAsync(Membership membership, CancellationToken cancellationToken)
        {
            // As the MongoDB driver: a cancelled token stops the save
            cancellationToken.ThrowIfCancellationRequested();
            Stored.RemoveAll(stored => stored.Id == membership.Id);
            Stored.Add(Copy(membership));
            return Task.CompletedTask;
        }

        public Task<Membership?> FindByMemberIdAsync(Guid memberId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Stored.Find(membership => membership.MemberId == memberId));
        }
    }
}
