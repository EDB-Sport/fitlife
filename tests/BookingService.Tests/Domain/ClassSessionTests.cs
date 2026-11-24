using BookingService.Domain;

namespace BookingService.Tests.Domain;

/// <summary>A class session never has more reservations than its capacity (#155), and a member has at most one of them (#156).</summary>
public sealed class ClassSessionTests
{
    private static ClassSession SessionWithSeats(int seats)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(seats));
    }

    [Test]
    public void Reserve_WhenClassSessionIsFull_IsRejected()
    {
        // Arrange
        ClassSession session = SessionWithSeats(1);
        session.Reserve(Guid.NewGuid());

        // Act
        ReservationResult result = session.Reserve(Guid.NewGuid());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ReservationResult.ClassSessionFull));
            Assert.That(session.Reservations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Reserve_WithFreeSeats_AddsTheMembersReservation()
    {
        // Arrange
        ClassSession session = SessionWithSeats(2);
        var memberId = Guid.NewGuid();

        // Act
        ReservationResult result = session.Reserve(memberId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ReservationResult.Made));
            Assert.That(session.Reservations.Select(reservation => reservation.MemberId), Is.EqualTo(new[] { memberId }));
        });
    }

    [Test]
    public void Reserve_TheLastFreeSeat_IsReserved()
    {
        // Arrange
        ClassSession session = SessionWithSeats(2);
        session.Reserve(Guid.NewGuid());

        // Act
        ReservationResult result = session.Reserve(Guid.NewGuid());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ReservationResult.Made));
            Assert.That(session.Reservations, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Reserve_WhenTheMemberAlreadyHasAReservation_IsRejected()
    {
        // Arrange
        ClassSession session = SessionWithSeats(2);
        var memberId = Guid.NewGuid();
        session.Reserve(memberId);

        // Act
        ReservationResult result = session.Reserve(memberId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ReservationResult.AlreadyReserved));
            Assert.That(session.Reservations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Reserve_AgainOnAFullClassSession_SaysTheMemberAlreadyHasASeat()
    {
        // Arrange
        ClassSession session = SessionWithSeats(1);
        var memberId = Guid.NewGuid();
        session.Reserve(memberId);

        // Act
        ReservationResult result = session.Reserve(memberId);

        // Assert
        Assert.That(result, Is.EqualTo(ReservationResult.AlreadyReserved));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Capacity_LessThanOne_IsRejected(int seats)
    {
        // Act & Assert
        Assert.That(() => new Capacity(seats), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
