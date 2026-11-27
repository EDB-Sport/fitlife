using MembershipService.Application;
using MembershipService.Domain;
using Microsoft.Extensions.Options;

namespace MembershipService.Tests;

/// <summary>A member with Betaling mangler pays the missing fee, and the membership becomes Aktiv (UC18, #250).</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // fresh fakes for every test
public sealed class PayMembershipHandlerTests
{
    private const string AccountId = "account-1";
    private static readonly DateOnly StartDate = new(2026, 11, 24);

    private readonly Member _member = Member.Join(AccountId);
    private readonly FakeMembers _members = new();
    private readonly FakeMemberships _memberships = new();
    private readonly FakePaymentProvider _payments = new();

    public PayMembershipHandlerTests()
    {
        _members.Stored.Add(_member);
    }

    private PayMembershipHandler Handler()
    {
        return new PayMembershipHandler(_members, _memberships, _payments, Options.Create(new MembershipFeeSettings { MonthlyFeeDkk = 350m }));
    }

    private void GivenAMembership(MembershipStatus status)
    {
        _memberships.Stored.Add(Membership.Restore(Guid.NewGuid(), _member.Id, MembershipType.Standard, status, StartDate));
    }

    [Test]
    public async Task HandleAsync_WhenNothingIsOwed_DoesNotCharge()
    {
        // Arrange
        GivenAMembership(MembershipStatus.Active);

        // Act
        PayMembershipResult result = await Handler().HandleAsync(new PayMembershipCommand(AccountId), CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(PayMembershipResult.NothingIsOwed));
        Assert.That(_payments.Charges, Is.Empty);
        Assert.That(_memberships.Saves, Is.Zero);
    }

    [Test]
    public async Task HandleAsync_WhenTheChargeIsAccepted_SavesTheMembershipAsActive()
    {
        // Arrange
        GivenAMembership(MembershipStatus.PaymentMissing);

        // Act
        PayMembershipResult result = await Handler().HandleAsync(new PayMembershipCommand(AccountId), CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(PayMembershipResult.Paid));
        Assert.That(_payments.Charges, Is.EqualTo(new[] { (_member.Id, 350m) }));
        Assert.That(_memberships.Saves, Is.EqualTo(1));
        Assert.That(_memberships.Stored.Single().Status, Is.EqualTo(MembershipStatus.Active));
    }

    [Test]
    public async Task HandleAsync_WhenTheChargeIsDeclined_SavesNothing()
    {
        // Arrange
        GivenAMembership(MembershipStatus.PaymentMissing);
        _payments.Paid = false;

        // Act
        PayMembershipResult result = await Handler().HandleAsync(new PayMembershipCommand(AccountId), CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(PayMembershipResult.ChargeDeclined));
        Assert.That(_memberships.Saves, Is.Zero);
        Assert.That(_memberships.Stored.Single().Status, Is.EqualTo(MembershipStatus.PaymentMissing));
    }

    [Test]
    public async Task HandleAsync_WhenTheAccountHasNotJoined_IsNotAMember()
    {
        // Act
        PayMembershipResult result = await Handler().HandleAsync(new PayMembershipCommand("account-2"), CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(PayMembershipResult.NotAMember));
        Assert.That(_payments.Charges, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_WhenTheMemberHasNoMembership_IsNotAMember()
    {
        // Act: the member is stored, the membership is not
        PayMembershipResult result = await Handler().HandleAsync(new PayMembershipCommand(AccountId), CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(PayMembershipResult.NotAMember));
        Assert.That(_payments.Charges, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_WhenTheRequestIsCancelledDuringTheCharge_StillSavesTheMembershipAsActive()
    {
        // Arrange: the caller goes away after the provider has taken the charge (#544)
        GivenAMembership(MembershipStatus.PaymentMissing);
        using var request = new CancellationTokenSource();
        _payments.DuringCharge = request.Cancel;

        // Act
        PayMembershipResult result = await Handler().HandleAsync(new PayMembershipCommand(AccountId), request.Token);

        // Assert
        Assert.That(result, Is.EqualTo(PayMembershipResult.Paid));
        Assert.That(_memberships.Stored.Single().Status, Is.EqualTo(MembershipStatus.Active));
    }

    [Test]
    public async Task HandleAsync_WhenTheRequestIsCancelledBeforeTheCharge_ChargesNothing()
    {
        // Arrange
        GivenAMembership(MembershipStatus.PaymentMissing);
        using var request = new CancellationTokenSource();
        await request.CancelAsync().ConfigureAwait(false);

        // Act
        Func<Task> act = () => Handler().HandleAsync(new PayMembershipCommand(AccountId), request.Token);

        // Assert
        await Assert.ThatAsync(act, Throws.InstanceOf<OperationCanceledException>()).ConfigureAwait(false);
        Assert.That(_payments.Charges, Is.Empty);
    }

    private sealed class FakePaymentProvider : IPaymentProvider
    {
        public bool Paid { get; set; } = true;

        /// <summary>Runs while the charge is being made, before the provider answers.</summary>
        public Action? DuringCharge { get; set; }

        public List<(Guid MemberId, decimal AmountDkk)> Charges { get; } = [];

        public Task<bool> ChargeMembershipFeeAsync(Guid memberId, decimal amountDkk, CancellationToken cancellationToken)
        {
            Charges.Add((memberId, amountDkk));
            DuringCharge?.Invoke();
            return Task.FromResult(Paid);
        }
    }

    private sealed class FakeMembers : IMemberRepository
    {
        public List<Member> Stored { get; } = [];

        public Task<Member?> FindByAccountIdAsync(string accountId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Stored.Find(member => string.Equals(member.AccountId, accountId, StringComparison.Ordinal)));
        }

        public Task<bool> TryAddAsync(Member member, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Paying never adds a member.");
        }
    }

    private sealed class FakeMemberships : IMembershipRepository
    {
        public List<Membership> Stored { get; } = [];

        public int Saves { get; private set; }

        // Copies, as a database does: a change to a membership is stored only when it is saved
        private static Membership Copy(Membership membership)
        {
            return Membership.Restore(membership.Id, membership.MemberId, membership.Type, membership.Status, membership.StartDate);
        }

        public Task AddAsync(Membership membership, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Paying never adds a membership.");
        }

        public Task RemoveAsync(Guid membershipId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Paying never removes a membership.");
        }

        public Task SaveAsync(Membership membership, CancellationToken cancellationToken)
        {
            // As the MongoDB driver: a cancelled token stops the save
            cancellationToken.ThrowIfCancellationRequested();
            Saves++;
            Stored.RemoveAll(stored => stored.Id == membership.Id);
            Stored.Add(Copy(membership));
            return Task.CompletedTask;
        }

        public Task<Membership?> FindByMemberIdAsync(Guid memberId, CancellationToken cancellationToken)
        {
            Membership? stored = Stored.Find(membership => membership.MemberId == memberId);
            if (stored is null)
            {
                return Task.FromResult<Membership?>(null);
            }

            return Task.FromResult<Membership?>(Copy(stored));
        }
    }
}
