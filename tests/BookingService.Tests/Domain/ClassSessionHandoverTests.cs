using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.Tests.Domain;

/// <summary>
/// Invariants 4 and 6: a freed seat goes to the first waitlist entry only while the waitlist is open, also when the closing job
/// hasn't run (#610).
/// </summary>
public sealed class ClassSessionHandoverTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);
    private static readonly ClassSessionDetails Details = new(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60));

    [TestCase(120)] // exactly 2 hours before start: the instant the waitlist closes
    [TestCase(10)]
    public void CancelReservation_AfterTheWaitlistHasClosed_FreesTheSeatAndExpiresTheWaitlist(int minutesBeforeStart)
    {
        // Arrange: full, with one waiting member, and the closing job never ran
        var clock = new FakeTimeProvider(Start.AddDays(-1));
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(1), Start, Details);
        var memberId = Guid.NewGuid();
        var waitingMemberId = Guid.NewGuid();
        classSession.Reserve(memberId, clock);
        classSession.JoinWaitlist(waitingMemberId, clock);
        clock.SetUtcNow(Start.AddMinutes(-minutesBeforeStart));

        // Act
        CancellationResult result = classSession.CancelReservation(memberId, clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CancellationResult.Cancelled));
            Assert.That(classSession.Reservations, Is.Empty);
            Assert.That(classSession.Waitlist, Is.Empty);
            Assert.That(classSession.DomainEvents.OfType<ReservationMade>().Select(made => made.MemberId), Does.Not.Contain(waitingMemberId));
            Assert.That(classSession.DomainEvents.OfType<WaitlistEntryExpired>().Select(expired => expired.MemberId), Is.EqualTo(new[] { waitingMemberId }));
        });
    }

    [Test]
    public void CancelReservation_JustBeforeTheWaitlistCloses_GivesTheSeatToTheFirstWaitlistEntry()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start.AddDays(-1));
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(1), Start, Details);
        var memberId = Guid.NewGuid();
        var waitingMemberId = Guid.NewGuid();
        classSession.Reserve(memberId, clock);
        classSession.JoinWaitlist(waitingMemberId, clock);
        clock.SetUtcNow(Start.AddHours(-2).AddTicks(-1));

        // Act
        classSession.CancelReservation(memberId, clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(classSession.Reservations.Select(reservation => (reservation.MemberId, reservation.IsFromWaitlist)), Is.EqualTo(new[] { (waitingMemberId, true) }));
            Assert.That(classSession.Waitlist, Is.Empty);
        });
    }
}
