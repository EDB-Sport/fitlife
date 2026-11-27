using System.Globalization;
using MembershipService.Domain;

namespace MembershipService.Tests;

/// <summary>
/// A new membership is active from the day the member joins and the first fee is paid (TO-BE-kontekst §8, UC5).
/// A member pauses it for a period and can end the pause early (UC6, #248). A member cancels it with one month's notice (UC8, #249).
/// </summary>
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

    [TestCase(2026, 11, 30, MembershipStatus.Active)]
    [TestCase(2026, 12, 1, MembershipStatus.Paused)]
    [TestCase(2026, 12, 31, MembershipStatus.Paused)]
    [TestCase(2027, 1, 1, MembershipStatus.Active)]
    public void Pause_WhenActive_IsPausedOnlyInsideThePeriod(int year, int month, int day, MembershipStatus expected)
    {
        // Arrange
        Membership membership = ActiveMembership();

        // Act
        PauseResult result = membership.Pause(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Today);

        // Assert
        Assert.That(result, Is.EqualTo(PauseResult.Paused));
        Assert.That(membership.StatusOn(new DateOnly(year, month, day)), Is.EqualTo(expected));
    }

    [Test]
    public void Pause_StartingToday_IsPausedToday()
    {
        // Arrange
        Membership membership = ActiveMembership();

        // Act
        PauseResult result = membership.Pause(Today, Today.AddDays(1), Today);

        // Assert
        Assert.That(result, Is.EqualTo(PauseResult.Paused));
        Assert.That(membership.StatusOn(Today), Is.EqualTo(MembershipStatus.Paused));
    }

    [Test]
    public void Pause_WhenNotActive_IsRejected()
    {
        // Arrange
        var membership = Membership.Start(Guid.NewGuid(), Today);

        // Act
        PauseResult result = membership.Pause(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Today);

        // Assert
        Assert.That(result, Is.EqualTo(PauseResult.MembershipNotActive));
        Assert.That(membership.PausedFrom, Is.Null);
    }

    [Test]
    public void Pause_WhilePaused_IsRejected()
    {
        // Arrange
        Membership membership = ActiveMembership();
        membership.Pause(Today, new DateOnly(2026, 12, 31), Today);

        // Act
        PauseResult result = membership.Pause(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31), Today);

        // Assert
        Assert.That(result, Is.EqualTo(PauseResult.MembershipNotActive));
        Assert.That(membership.PausedTo, Is.EqualTo(new DateOnly(2026, 12, 31)));
    }

    [Test]
    public void Pause_StartingBeforeToday_IsRejected()
    {
        // Arrange
        Membership membership = ActiveMembership();

        // Act
        PauseResult result = membership.Pause(Today.AddDays(-1), new DateOnly(2026, 12, 31), Today);

        // Assert
        Assert.That(result, Is.EqualTo(PauseResult.PauseStartsInThePast));
        Assert.That(membership.PausedFrom, Is.Null);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Pause_EndingOnOrBeforeItsStart_IsRejected(int daysAfterStart)
    {
        // Arrange
        Membership membership = ActiveMembership();
        var from = new DateOnly(2026, 12, 1);

        // Act
        PauseResult result = membership.Pause(from, from.AddDays(daysAfterStart), Today);

        // Assert
        Assert.That(result, Is.EqualTo(PauseResult.PauseEndsBeforeItStarts));
        Assert.That(membership.PausedFrom, Is.Null);
    }

    [Test]
    public void Pause_WithAPauseStillToCome_ReplacesIt()
    {
        // Arrange
        Membership membership = ActiveMembership();
        membership.Pause(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Today);

        // Act
        PauseResult result = membership.Pause(new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 28), Today);

        // Assert
        Assert.That(result, Is.EqualTo(PauseResult.Paused));
        Assert.That(membership.StatusOn(new DateOnly(2026, 12, 15)), Is.EqualTo(MembershipStatus.Active));
        Assert.That(membership.StatusOn(new DateOnly(2027, 2, 15)), Is.EqualTo(MembershipStatus.Paused));
    }

    [Test]
    public void EndPause_WhilePaused_MakesTheMembershipActiveFromToday()
    {
        // Arrange
        Membership membership = ActiveMembership();
        membership.Pause(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Today);
        var today = new DateOnly(2026, 12, 10);

        // Act
        EndPauseResult result = membership.EndPause(today);

        // Assert
        Assert.That(result, Is.EqualTo(EndPauseResult.PauseEnded));
        Assert.That(membership.StatusOn(today), Is.EqualTo(MembershipStatus.Active));
    }

    [Test]
    public void EndPause_OnTheLastDayOfThePause_EndsIt()
    {
        // Arrange
        Membership membership = ActiveMembership();
        membership.Pause(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Today);
        var today = new DateOnly(2026, 12, 31);

        // Act
        EndPauseResult result = membership.EndPause(today);

        // Assert
        Assert.That(result, Is.EqualTo(EndPauseResult.PauseEnded));
        Assert.That(membership.StatusOn(today), Is.EqualTo(MembershipStatus.Active));
    }

    [Test]
    public void EndPause_BeforeThePauseStarts_RemovesIt()
    {
        // Arrange
        Membership membership = ActiveMembership();
        membership.Pause(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Today);

        // Act
        EndPauseResult result = membership.EndPause(Today);

        // Assert
        Assert.That(result, Is.EqualTo(EndPauseResult.PauseEnded));
        Assert.That(membership.PausedFrom, Is.Null);
        Assert.That(membership.PausedTo, Is.Null);
        Assert.That(membership.StatusOn(new DateOnly(2026, 12, 15)), Is.EqualTo(MembershipStatus.Active));
    }

    [Test]
    public void EndPause_WithNoPause_IsRejected()
    {
        // Arrange
        Membership membership = ActiveMembership();

        // Act
        EndPauseResult result = membership.EndPause(Today);

        // Assert
        Assert.That(result, Is.EqualTo(EndPauseResult.NoPauseToEnd));
    }

    [Test]
    public void EndPause_AfterThePauseIsOver_IsRejected()
    {
        // Arrange
        Membership membership = ActiveMembership();
        membership.Pause(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Today);

        // Act
        EndPauseResult result = membership.EndPause(new DateOnly(2027, 1, 1));

        // Assert
        Assert.That(result, Is.EqualTo(EndPauseResult.NoPauseToEnd));
        Assert.That(membership.PausedTo, Is.EqualTo(new DateOnly(2026, 12, 31)));
    }

    [Test]
    public void IsActive_WhilePaused_IsFalse()
    {
        // Arrange
        Membership membership = ActiveMembership();

        // Act
        membership.Pause(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Today);

        // Assert
        Assert.That(membership.IsActiveOn(new DateOnly(2026, 12, 1)), Is.False);
        Assert.That(membership.IsActiveOn(new DateOnly(2026, 11, 30)), Is.True);
    }

    [Test]
    public void StatusOn_WithPaymentMissing_IsPaymentMissing()
    {
        // Arrange
        var membership = Membership.Start(Guid.NewGuid(), Today);

        // Act
        MembershipStatus status = membership.StatusOn(Today);

        // Assert
        Assert.That(status, Is.EqualTo(MembershipStatus.PaymentMissing));
        Assert.That(membership.IsActiveOn(Today), Is.False);
    }

    [TestCase("2026-11-24", "2026-12-31")]
    [TestCase("2026-11-30", "2026-12-31")]
    [TestCase("2026-12-01", "2027-01-31")]
    [TestCase("2026-12-31", "2027-01-31")]
    [TestCase("2027-01-31", "2027-02-28")]
    [TestCase("2028-01-31", "2028-02-29")]
    [TestCase("2028-02-29", "2028-03-31")]
    public void Cancel_OnADay_SetsTheLastTrainingDayToTheEndOfTheNextMonth(string cancelledOn, string lastTrainingDay)
    {
        // Arrange
        Membership membership = ActiveMembership();
        var today = DateOnly.Parse(cancelledOn, CultureInfo.InvariantCulture);

        // Act
        CancelResult result = membership.Cancel(today);

        // Assert
        Assert.That(result, Is.EqualTo(CancelResult.Cancelled));
        Assert.That(membership.LastTrainingDay, Is.EqualTo(DateOnly.Parse(lastTrainingDay, CultureInfo.InvariantCulture)));
        Assert.That(membership.StatusOn(today), Is.EqualTo(MembershipStatus.Cancelled));
    }

    [Test]
    public void Cancel_WhilePaused_IsCancelled()
    {
        // Arrange
        Membership membership = ActiveMembership();
        membership.Pause(Today, new DateOnly(2026, 12, 15), Today);

        // Act
        CancelResult result = membership.Cancel(Today);

        // Assert
        Assert.That(result, Is.EqualTo(CancelResult.Cancelled));
        Assert.That(membership.StatusOn(new DateOnly(2026, 12, 1)), Is.EqualTo(MembershipStatus.Cancelled));
        Assert.That(membership.LastTrainingDay, Is.EqualTo(new DateOnly(2026, 12, 31)));
    }

    [Test]
    public void Cancel_WhenAlreadyCancelled_IsRejected()
    {
        // Arrange
        Membership membership = ActiveMembership();
        membership.Cancel(Today);

        // Act
        CancelResult result = membership.Cancel(new DateOnly(2026, 12, 1));

        // Assert
        Assert.That(result, Is.EqualTo(CancelResult.MembershipAlreadyCancelled));
        Assert.That(membership.LastTrainingDay, Is.EqualTo(new DateOnly(2026, 12, 31)));
    }

    [Test]
    public void Cancel_AfterTheLastTrainingDay_IsRejected()
    {
        // Arrange
        Membership membership = ActiveMembership();
        membership.Cancel(Today);

        // Act
        CancelResult result = membership.Cancel(new DateOnly(2027, 1, 1));

        // Assert
        Assert.That(result, Is.EqualTo(CancelResult.MembershipAlreadyCancelled));
        Assert.That(membership.LastTrainingDay, Is.EqualTo(new DateOnly(2026, 12, 31)));
    }

    [Test]
    public void StatusOn_OnTheLastTrainingDay_IsCancelled()
    {
        // Arrange
        Membership membership = ActiveMembership();
        membership.Cancel(Today);

        // Act
        MembershipStatus status = membership.StatusOn(new DateOnly(2026, 12, 31));

        // Assert
        Assert.That(status, Is.EqualTo(MembershipStatus.Cancelled));
    }

    [Test]
    public void StatusOn_TheDayAfterTheLastTrainingDay_IsEnded()
    {
        // Arrange
        Membership membership = ActiveMembership();
        membership.Cancel(Today);

        // Act
        MembershipStatus status = membership.StatusOn(new DateOnly(2027, 1, 1));

        // Assert
        Assert.That(status, Is.EqualTo(MembershipStatus.Ended));
    }

    [Test]
    public void IsActive_AfterCancelling_IsFalse()
    {
        // Arrange
        Membership membership = ActiveMembership();

        // Act
        membership.Cancel(Today);

        // Assert
        Assert.That(membership.IsActive, Is.False);
        Assert.That(membership.IsActiveOn(Today), Is.False);
    }

    private static Membership ActiveMembership()
    {
        var membership = Membership.Start(Guid.NewGuid(), Today);
        membership.MarkPaid();
        return membership;
    }
}
