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

    [Test]
    public void MarkPaymentMissing_ActiveMembership_IsPaymentMissingAndNotActive()
    {
        // Arrange
        var membership = Membership.Start(Guid.NewGuid(), Today);
        membership.MarkPaid();

        // Act
        membership.MarkPaymentMissing();

        // Assert
        Assert.That(membership.Status, Is.EqualTo(MembershipStatus.PaymentMissing));
        Assert.That(membership.IsActive, Is.False);
    }
}
