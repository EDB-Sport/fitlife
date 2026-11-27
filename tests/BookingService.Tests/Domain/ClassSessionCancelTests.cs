using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.Tests.Domain;

/// <summary>Invariant 8 of the <see cref="ClassSession"/> aggregate: a cancelled class session (src/BookingService/CONTEXT.md).</summary>
public sealed class ClassSessionCancelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ClassSessionDetails Details = new(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60));
    private readonly FakeTimeProvider _clock = new(Now);

    private static ClassSession ClassSessionWithCapacity(int capacity)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(capacity), Now.AddDays(1), Details);
    }

    [Test]
    public void Cancel_RemovesEveryReservationAndWaitlistEntryAndNamesTheirMembers()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        var memberId = Guid.NewGuid();
        var waitingMemberId = Guid.NewGuid();
        classSession.Reserve(memberId, _clock);
        classSession.JoinWaitlist(waitingMemberId, _clock);

        // Act
        classSession.Cancel(_clock);

        // Assert
        var classSessionCancelled = (ClassSessionCancelled)classSession.DomainEvents[^1];
        Assert.Multiple(() =>
        {
            Assert.That(classSession.IsCancelled, Is.True);
            Assert.That(classSession.Reservations, Is.Empty);
            Assert.That(classSession.Waitlist, Is.Empty);
            Assert.That(classSessionCancelled.ClassSessionId, Is.EqualTo(classSession.Id));
            Assert.That(classSessionCancelled.MemberIds, Is.EqualTo(new[] { memberId, waitingMemberId }));
        });
    }

    [TestCase(0)] // exactly at the start
    [TestCase(30)] // while it runs
    public void Cancel_AfterTheClassSessionHasStarted_IsRejectedAndKeepsTheReservations(int minutesAfterStart)
    {
        // Arrange: #538, the start is the cut-off as in #312
        var clock = new FakeTimeProvider(Now); // its own: the fixture's clock is shared between tests
        ClassSession classSession = ClassSessionWithCapacity(1);
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, clock);
        int eventsBefore = classSession.DomainEvents.Count;
        clock.SetUtcNow(classSession.StartTime.AddMinutes(minutesAfterStart));

        // Act
        CancelResult result = classSession.Cancel(clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CancelResult.ClassSessionStarted));
            Assert.That(classSession.IsCancelled, Is.False);
            Assert.That(classSession.Reservations.Select(reservation => reservation.MemberId), Is.EqualTo(new[] { memberId }));
            Assert.That(classSession.DomainEvents, Has.Count.EqualTo(eventsBefore));
        });
    }

    [Test]
    public void Cancel_JustBeforeTheStart_Cancels()
    {
        // Arrange: a clock of its own, because the fixture's clock is shared between tests
        var clock = new FakeTimeProvider(Now);
        ClassSession classSession = ClassSessionWithCapacity(1);
        clock.SetUtcNow(classSession.StartTime.AddTicks(-1));

        // Act
        CancelResult result = classSession.Cancel(clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CancelResult.Cancelled));
            Assert.That(classSession.IsCancelled, Is.True);
        });
    }

    [Test]
    public void Reserve_OnACancelledClassSession_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Cancel(_clock);

        // Act
        ReservationResult result = classSession.Reserve(Guid.NewGuid(), _clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ReservationResult.ClassSessionCancelled));
            Assert.That(classSession.Reservations, Is.Empty);
        });
    }

    [Test]
    public void Cancel_OnACancelledClassSession_RaisesNoSecondClassSessionCancelled()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Reserve(Guid.NewGuid(), _clock);
        classSession.Cancel(_clock);

        // Act
        classSession.Cancel(_clock);

        // Assert
        Assert.That(classSession.DomainEvents.OfType<ClassSessionCancelled>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void JoinWaitlist_OnACancelledClassSession_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Cancel(_clock);

        // Act
        WaitlistResult result = classSession.JoinWaitlist(Guid.NewGuid(), _clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(WaitlistResult.ClassSessionCancelled));
            Assert.That(classSession.Waitlist, Is.Empty);
        });
    }
}
