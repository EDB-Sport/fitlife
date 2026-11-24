using BookingService.Domain;

namespace BookingService.Tests.Domain;

/// <summary>A class session never has more reservations than its capacity (#155), and a member has at most one of them (#156).</summary>
public sealed class ClassSessionTests
{
    private static ClassSession ClassSessionWithCapacity(int capacity)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(capacity));
    }

    [Test]
    public void Reserve_WhenClassSessionIsFull_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Reserve(Guid.NewGuid());

        // Act
        ReservationResult result = classSession.Reserve(Guid.NewGuid());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ReservationResult.ClassSessionFull));
            Assert.That(classSession.Reservations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Reserve_WithFreeSeats_AddsTheMembersReservation()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(2);
        var memberId = Guid.NewGuid();

        // Act
        ReservationResult result = classSession.Reserve(memberId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ReservationResult.Made));
            Assert.That(classSession.Reservations.Select(reservation => reservation.MemberId), Is.EqualTo(new[] { memberId }));
        });
    }

    [Test]
    public void Reserve_TheLastFreeSeat_IsReserved()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(2);
        classSession.Reserve(Guid.NewGuid());

        // Act
        ReservationResult result = classSession.Reserve(Guid.NewGuid());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ReservationResult.Made));
            Assert.That(classSession.Reservations, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Reserve_WhenTheMemberAlreadyHasAReservation_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(2);
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId);

        // Act
        ReservationResult result = classSession.Reserve(memberId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ReservationResult.MemberAlreadyHasReservation));
            Assert.That(classSession.Reservations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Reserve_AgainOnAFullClassSession_SaysTheMemberAlreadyHasASeat()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId);

        // Act
        ReservationResult result = classSession.Reserve(memberId);

        // Assert
        Assert.That(result, Is.EqualTo(ReservationResult.MemberAlreadyHasReservation));
    }

    [Test]
    public void JoinWaitlist_WhenClassSessionIsFull_AddsWaitlistEntriesInTheOrderTheyWereMade()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Reserve(Guid.NewGuid());
        var firstMemberId = Guid.NewGuid();
        var secondMemberId = Guid.NewGuid();
        classSession.JoinWaitlist(firstMemberId);

        // Act
        WaitlistResult result = classSession.JoinWaitlist(secondMemberId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(WaitlistResult.Joined));
            Assert.That(classSession.Waitlist.Select(waitlistEntry => waitlistEntry.MemberId), Is.EqualTo(new[] { firstMemberId, secondMemberId }));
        });
    }

    [Test]
    public void JoinWaitlist_WithFreeSeats_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);

        // Act
        WaitlistResult result = classSession.JoinWaitlist(Guid.NewGuid());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(WaitlistResult.ClassSessionNotFull));
            Assert.That(classSession.Waitlist, Is.Empty);
        });
    }

    [Test]
    public void JoinWaitlist_WhenTheMemberAlreadyHasAReservation_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId);

        // Act
        WaitlistResult result = classSession.JoinWaitlist(memberId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(WaitlistResult.MemberAlreadyHasReservation));
            Assert.That(classSession.Waitlist, Is.Empty);
        });
    }

    [Test]
    public void JoinWaitlist_WhenTheMemberIsAlreadyOnTheWaitlist_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Reserve(Guid.NewGuid());
        var memberId = Guid.NewGuid();
        classSession.JoinWaitlist(memberId);

        // Act
        WaitlistResult result = classSession.JoinWaitlist(memberId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(WaitlistResult.MemberAlreadyOnWaitlist));
            Assert.That(classSession.Waitlist, Has.Count.EqualTo(1));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Capacity_LessThanOne_IsRejected(int value)
    {
        // Act & Assert
        Assert.That(() => new Capacity(value), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
