using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.Tests.Domain;

/// <summary>The invariants of the <see cref="ClassSession"/> aggregate (src/BookingService/CONTEXT.md).</summary>
public sealed class ClassSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ClassSessionDetails Details = new(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60));
    private readonly FakeTimeProvider _clock = new(Now);

    private static ClassSession ClassSessionWithCapacity(int capacity)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(capacity), Now.AddDays(1), Details);
    }

    private static ClassSession ClassSessionStartingAt(DateTimeOffset startTime)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(1), startTime, Details);
    }

    [Test]
    public void Reserve_WhenClassSessionIsFull_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Reserve(Guid.NewGuid(), _clock);

        // Act
        ReservationResult result = classSession.Reserve(Guid.NewGuid(), _clock);

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
        ReservationResult result = classSession.Reserve(memberId, _clock);

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
        classSession.Reserve(Guid.NewGuid(), _clock);

        // Act
        ReservationResult result = classSession.Reserve(Guid.NewGuid(), _clock);

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
        classSession.Reserve(memberId, _clock);

        // Act
        ReservationResult result = classSession.Reserve(memberId, _clock);

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
        classSession.Reserve(memberId, _clock);

        // Act
        ReservationResult result = classSession.Reserve(memberId, _clock);

        // Assert
        Assert.That(result, Is.EqualTo(ReservationResult.MemberAlreadyHasReservation));
    }

    [Test]
    public void JoinWaitlist_WhenClassSessionIsFull_AddsWaitlistEntriesInTheOrderTheyWereMade()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Reserve(Guid.NewGuid(), _clock);
        var firstMemberId = Guid.NewGuid();
        var secondMemberId = Guid.NewGuid();
        classSession.JoinWaitlist(firstMemberId, _clock);

        // Act
        WaitlistResult result = classSession.JoinWaitlist(secondMemberId, _clock);

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
        WaitlistResult result = classSession.JoinWaitlist(Guid.NewGuid(), _clock);

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
        classSession.Reserve(memberId, _clock);

        // Act
        WaitlistResult result = classSession.JoinWaitlist(memberId, _clock);

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
        classSession.Reserve(Guid.NewGuid(), _clock);
        var memberId = Guid.NewGuid();
        classSession.JoinWaitlist(memberId, _clock);

        // Act
        WaitlistResult result = classSession.JoinWaitlist(memberId, _clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(WaitlistResult.MemberAlreadyOnWaitlist));
            Assert.That(classSession.Waitlist, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void CancelReservation_OnAFullClassSession_GivesTheSeatToTheFirstWaitlistEntry()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        var memberId = Guid.NewGuid();
        var firstWaitingMemberId = Guid.NewGuid();
        var secondWaitingMemberId = Guid.NewGuid();
        classSession.Reserve(memberId, _clock);
        classSession.JoinWaitlist(firstWaitingMemberId, _clock);
        classSession.JoinWaitlist(secondWaitingMemberId, _clock);

        // Act
        CancellationResult result = classSession.CancelReservation(memberId, _clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CancellationResult.Cancelled));
            Assert.That(classSession.Reservations.Select(reservation => reservation.MemberId), Is.EqualTo(new[] { firstWaitingMemberId }));
            Assert.That(classSession.Waitlist.Select(waitlistEntry => waitlistEntry.MemberId), Is.EqualTo(new[] { secondWaitingMemberId }));
            Assert.That(
                classSession.DomainEvents,
                Is.EqualTo(new DomainEvent[]
                {
                    new ReservationCancelled(classSession.Id, memberId, IsLateCancellation: false),
                    new ReservationMade(classSession.Id, firstWaitingMemberId),
                }));
        });
    }

    [Test]
    public void CancelReservation_WithAnEmptyWaitlist_FreesTheSeat()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, _clock);

        // Act
        CancellationResult result = classSession.CancelReservation(memberId, _clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CancellationResult.Cancelled));
            Assert.That(classSession.Reservations, Is.Empty);
            Assert.That(classSession.DomainEvents, Is.EqualTo(new DomainEvent[] { new ReservationCancelled(classSession.Id, memberId, IsLateCancellation: false) }));
        });
    }

    [Test]
    public void CancelReservation_WhenTheMemberHasNoReservation_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Reserve(Guid.NewGuid(), _clock);

        // Act
        CancellationResult result = classSession.CancelReservation(Guid.NewGuid(), _clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CancellationResult.NoReservation));
            Assert.That(classSession.Reservations, Has.Count.EqualTo(1));
            Assert.That(classSession.DomainEvents, Is.Empty);
        });
    }

    [Test]
    public void Reserve_MoreThanFourWeeksAhead_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddDays(28).AddMinutes(1));

        // Act
        ReservationResult result = classSession.Reserve(Guid.NewGuid(), _clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ReservationResult.TooFarAhead));
            Assert.That(classSession.Reservations, Is.Empty);
        });
    }

    [Test]
    public void Reserve_ExactlyFourWeeksAhead_IsReserved()
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddDays(28));

        // Act
        ReservationResult result = classSession.Reserve(Guid.NewGuid(), _clock);

        // Assert
        Assert.That(result, Is.EqualTo(ReservationResult.Made));
    }

    [Test]
    public void JoinWaitlist_LessThanTwoHoursBeforeStart_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddHours(2).AddMinutes(-1));
        classSession.Reserve(Guid.NewGuid(), _clock);

        // Act
        WaitlistResult result = classSession.JoinWaitlist(Guid.NewGuid(), _clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(WaitlistResult.WaitlistClosed));
            Assert.That(classSession.Waitlist, Is.Empty);
        });
    }

    [Test]
    public void JoinWaitlist_ExactlyTwoHoursBeforeStart_IsJoined()
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddHours(2));
        classSession.Reserve(Guid.NewGuid(), _clock);

        // Act
        WaitlistResult result = classSession.JoinWaitlist(Guid.NewGuid(), _clock);

        // Assert
        Assert.That(result, Is.EqualTo(WaitlistResult.Joined));
    }

    [Test]
    public void CloseWaitlist_TwoHoursBeforeStart_RemovesTheWaitlistEntriesAndTellsEachMember()
    {
        // Arrange
        var clock = new FakeTimeProvider(Now);
        ClassSession classSession = ClassSessionStartingAt(Now.AddHours(3));
        var firstWaitingMemberId = Guid.NewGuid();
        var secondWaitingMemberId = Guid.NewGuid();
        classSession.Reserve(Guid.NewGuid(), clock);
        classSession.JoinWaitlist(firstWaitingMemberId, clock);
        classSession.JoinWaitlist(secondWaitingMemberId, clock);
        clock.Advance(TimeSpan.FromHours(1));

        // Act
        WaitlistClosingResult result = classSession.CloseWaitlist(clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(WaitlistClosingResult.Closed));
            Assert.That(classSession.Waitlist, Is.Empty);
            Assert.That(
                classSession.DomainEvents,
                Is.EqualTo(new DomainEvent[]
                {
                    new WaitlistEntryExpired(classSession.Id, firstWaitingMemberId),
                    new WaitlistEntryExpired(classSession.Id, secondWaitingMemberId),
                }));
        });
    }

    [Test]
    public void CloseWaitlist_MoreThanTwoHoursBeforeStart_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddHours(3));
        classSession.Reserve(Guid.NewGuid(), _clock);
        classSession.JoinWaitlist(Guid.NewGuid(), _clock);

        // Act
        WaitlistClosingResult result = classSession.CloseWaitlist(_clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(WaitlistClosingResult.TooEarly));
            Assert.That(classSession.Waitlist, Has.Count.EqualTo(1));
            Assert.That(classSession.DomainEvents, Is.Empty);
        });
    }

    [Test]
    public void CancelReservation_LessThanTwoHoursBeforeStart_IsALateCancellation()
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddHours(2).AddMinutes(-1));
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, _clock);

        // Act
        classSession.CancelReservation(memberId, _clock);

        // Assert
        Assert.That(classSession.DomainEvents, Is.EqualTo(new DomainEvent[] { new ReservationCancelled(classSession.Id, memberId, IsLateCancellation: true) }));
    }

    [Test]
    public void CancelReservation_ExactlyTwoHoursBeforeStart_IsNotALateCancellation()
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddHours(2));
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, _clock);

        // Act
        classSession.CancelReservation(memberId, _clock);

        // Assert
        Assert.That(classSession.DomainEvents, Is.EqualTo(new DomainEvent[] { new ReservationCancelled(classSession.Id, memberId, IsLateCancellation: false) }));
    }

    [Test]
    public void CancelReservation_OfASeatFromTheWaitlist_IsNeverALateCancellation()
    {
        // Arrange
        var clock = new FakeTimeProvider(Now);
        ClassSession classSession = ClassSessionStartingAt(Now.AddHours(3));
        var waitingMemberId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, clock);
        classSession.JoinWaitlist(waitingMemberId, clock);
        classSession.CancelReservation(memberId, clock);
        clock.Advance(TimeSpan.FromHours(2));

        // Act
        classSession.CancelReservation(waitingMemberId, clock);

        // Assert
        Assert.That(classSession.DomainEvents[^1], Is.EqualTo(new ReservationCancelled(classSession.Id, waitingMemberId, IsLateCancellation: false)));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Capacity_LessThanOne_IsRejected(int value)
    {
        // Act & Assert
        Assert.That(() => new Capacity(value), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
