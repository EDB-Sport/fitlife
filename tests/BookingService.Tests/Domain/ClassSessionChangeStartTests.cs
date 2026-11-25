using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.Tests.Domain;

/// <summary>A class session moved to a new start keeps its reservations, and they cancel free (#240, CONTEXT.md invariant 9).</summary>
public sealed class ClassSessionChangeStartTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ClassSessionDetails Details = new(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60));
    private readonly FakeTimeProvider _clock = new(Now);

    private static ClassSession ClassSessionStartingAt(DateTimeOffset startTime)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(1), startTime, Details);
    }

    [Test]
    public void ChangeStart_ThenAMemberWhoHadAReservationCancelsUnderTwoHoursBeforeStart_IsNotALateCancellation()
    {
        // Arrange
        var clock = new FakeTimeProvider(Now);
        ClassSession classSession = ClassSessionStartingAt(Now.AddDays(1));
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, clock);
        classSession.ChangeStart(Now.AddHours(1), clock);

        // Act
        classSession.CancelReservation(memberId, clock);

        // Assert
        Assert.That(classSession.DomainEvents[^1], Is.EqualTo(new ReservationCancelled(classSession.Id, memberId, IsLateCancellation: false)));
    }

    [Test]
    public void ChangeStart_ThenAMemberWhoReservedAfterwardsCancelsUnderTwoHoursBeforeStart_IsALateCancellation()
    {
        // Arrange
        var clock = new FakeTimeProvider(Now);
        ClassSession classSession = ClassSessionStartingAt(Now.AddDays(1));
        classSession.ChangeStart(Now.AddHours(1), clock);
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, clock);

        // Act
        classSession.CancelReservation(memberId, clock);

        // Assert
        Assert.That(classSession.DomainEvents[^1], Is.EqualTo(new ReservationCancelled(classSession.Id, memberId, IsLateCancellation: true)));
    }

    [Test]
    public void ChangeStart_WithReservationsAndWaitlistEntries_KeepsThem()
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddDays(1));
        var memberId = Guid.NewGuid();
        var waitingMemberId = Guid.NewGuid();
        classSession.Reserve(memberId, _clock);
        classSession.JoinWaitlist(waitingMemberId, _clock);
        DateTimeOffset previousStart = classSession.StartTime;

        // Act
        ChangeStartResult result = classSession.ChangeStart(Now.AddDays(2), _clock);

        // Assert
        var classSessionChanged = (ClassSessionChanged)classSession.DomainEvents[^1];
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ChangeStartResult.Changed));
            Assert.That(classSession.StartTime, Is.EqualTo(Now.AddDays(2)));
            Assert.That(classSession.Reservations.Select(reservation => reservation.MemberId), Is.EqualTo(new[] { memberId }));
            Assert.That(classSession.Waitlist.Select(waitlistEntry => waitlistEntry.MemberId), Is.EqualTo(new[] { waitingMemberId }));
            Assert.That(classSessionChanged.ClassSessionId, Is.EqualTo(classSession.Id));
            Assert.That(classSessionChanged.MemberIds, Is.EqualTo(new[] { memberId }));
            Assert.That(classSessionChanged.PreviousStartTime, Is.EqualTo(previousStart));
            Assert.That(classSessionChanged.StartTime, Is.EqualTo(Now.AddDays(2)));
        });
    }

    [Test]
    public void ChangeStart_OnACancelledClassSession_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddDays(1));
        classSession.Cancel();
        int events = classSession.DomainEvents.Count;

        // Act
        ChangeStartResult result = classSession.ChangeStart(Now.AddDays(2), _clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ChangeStartResult.ClassSessionCancelled));
            Assert.That(classSession.StartTime, Is.EqualTo(Now.AddDays(1)));
            Assert.That(classSession.DomainEvents, Has.Count.EqualTo(events));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ChangeStart_ToATimeThatIsNotInTheFuture_IsRejected(int minutesFromNow)
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddDays(1));
        classSession.Reserve(Guid.NewGuid(), _clock);

        // Act
        ChangeStartResult result = classSession.ChangeStart(Now.AddMinutes(minutesFromNow), _clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ChangeStartResult.StartNotInTheFuture));
            Assert.That(classSession.StartTime, Is.EqualTo(Now.AddDays(1)));
            Assert.That(classSession.DomainEvents, Is.Empty);
        });
    }
}
