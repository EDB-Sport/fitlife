using BookingService.Domain;

namespace BookingService.Tests.Domain;

/// <summary>A class session never has more reservations than its capacity (issue #155).</summary>
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

    [TestCase(0)]
    [TestCase(-1)]
    public void Capacity_LessThanOne_IsRejected(int value)
    {
        // Act & Assert
        Assert.That(() => new Capacity(value), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
