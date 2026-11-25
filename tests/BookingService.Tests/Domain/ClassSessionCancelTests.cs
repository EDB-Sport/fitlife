using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.Tests.Domain;

/// <summary>Invariant 8 of the <see cref="ClassSession"/> aggregate: a cancelled class session (src/BookingService/CONTEXT.md).</summary>
public sealed class ClassSessionCancelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _clock = new(Now);

    private static ClassSession ClassSessionWithCapacity(int capacity)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(capacity), Now.AddDays(1));
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
        classSession.Cancel();

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

    [Test]
    public void Reserve_OnACancelledClassSession_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Cancel();

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
        classSession.Cancel();

        // Act
        classSession.Cancel();

        // Assert
        Assert.That(classSession.DomainEvents.OfType<ClassSessionCancelled>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void JoinWaitlist_OnACancelledClassSession_IsRejected()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Cancel();

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
