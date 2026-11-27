using MembershipService.Domain;

namespace MembershipService.Tests;

/// <summary>A new membership is active from the day the member joins and the first fee is paid (TO-BE-kontekst §8, UC5).</summary>
public sealed class MembershipTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);

    [Test]
    public void Start_NewMembership_HasPaymentMissingUntilTheFirstFeeIsPaid()
    {
        // Arrange
        var memberId = Guid.NewGuid();

        // Act
        var membership = Membership.Start(memberId, Today);

        // Assert
        Assert.That(membership.Status, Is.EqualTo(MembershipStatus.PaymentMissing));
        Assert.That(membership.IsActive, Is.False);
    }

    [Test]
    public void MarkPaid_NewMembership_IsActive()
    {
        // Arrange
        var membership = Membership.Start(Guid.NewGuid(), Today);

        // Act
        membership.MarkPaid();

        // Assert
        Assert.That(membership.Status, Is.EqualTo(MembershipStatus.Active));
        Assert.That(membership.IsActive, Is.True);
    }

    [Test]
    public void RegisterPayment_WhenPaymentIsMissing_MakesTheMembershipActive()
    {
        // Arrange
        var membership = Membership.Start(Guid.NewGuid(), Today);

        // Act
        PaymentResult result = membership.RegisterPayment();

        // Assert
        Assert.That(result, Is.EqualTo(PaymentResult.Paid));
        Assert.That(membership.Status, Is.EqualTo(MembershipStatus.Active));
    }

    [Test]
    public void RegisterPayment_WhenNothingIsOwed_IsRejected()
    {
        // Arrange
        var membership = Membership.Restore(Guid.NewGuid(), Guid.NewGuid(), MembershipType.Standard, MembershipStatus.Active, Today);

        // Act
        PaymentResult result = membership.RegisterPayment();

        // Assert
        Assert.That(result, Is.EqualTo(PaymentResult.NothingIsOwed));
        Assert.That(membership.Status, Is.EqualTo(MembershipStatus.Active));
    }

    [Test]
    public void Start_NewMembership_HasTheOneMembershipTypeAndStartsToday()
    {
        // Arrange
        var memberId = Guid.NewGuid();

        // Act
        var membership = Membership.Start(memberId, Today);

        // Assert
        Assert.That(membership.Type, Is.EqualTo(MembershipType.Standard));
        Assert.That(membership.StartDate, Is.EqualTo(Today));
        Assert.That(membership.MemberId, Is.EqualTo(memberId));
    }
}
