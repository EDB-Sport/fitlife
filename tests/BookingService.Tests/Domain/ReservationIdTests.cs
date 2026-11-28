using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.Tests.Domain;

/// <summary>Every reservation has its own id, which <c>ReservationMade</c> carries to the other contexts (#192).</summary>
public sealed class ReservationIdTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);

    [Test]
    public void Reserve_TwoMembers_GivesEachReservationItsOwnId()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(2);

        // Act
        classSession.Reserve(Guid.NewGuid(), _clock);
        classSession.Reserve(Guid.NewGuid(), _clock);

        // Assert
        Guid[] ids = classSession.Reservations.Select(reservation => reservation.Id).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(ids, Has.None.EqualTo(Guid.Empty));
            Assert.That(ids, Is.Unique);
        });
    }

    [Test]
    public void CancelReservation_WithAWaitlist_GivesTheSeatFromTheWaitlistANewId()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, _clock);
        Guid cancelledId = classSession.Reservations.Single().Id;
        classSession.JoinWaitlist(Guid.NewGuid(), _clock);

        // Act
        classSession.CancelReservation(memberId, _clock);

        // Assert
        Guid newId = classSession.Reservations.Single().Id;
        Assert.That(newId, Is.Not.EqualTo(cancelledId).And.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public void ChangeStart_KeepsTheReservationIds()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(1);
        classSession.Reserve(Guid.NewGuid(), _clock);
        Guid id = classSession.Reservations.Single().Id;

        // Act
        classSession.ChangeStart(Now.AddDays(2), _clock);

        // Assert
        Assert.That(classSession.Reservations.Single().Id, Is.EqualTo(id));
    }

    private static ClassSession ClassSessionWithCapacity(int capacity)
    {
        return new ClassSession(
            Guid.NewGuid(), new Capacity(capacity), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
    }
}
