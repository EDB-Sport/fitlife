using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.Tests.Domain;

/// <summary>A class session that has started can't be reserved; until its start it can (#312, CONTEXT.md invariant 10).</summary>
public sealed class ClassSessionStartedTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ClassSessionDetails Details = new(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60));
    private readonly FakeTimeProvider _clock = new(Now);

    private static ClassSession ClassSessionStartingAt(DateTimeOffset startTime)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(1), startTime, Details);
    }

    [TestCase(0)]
    [TestCase(-30)]
    public void Reserve_AfterTheClassSessionHasStarted_IsRejected(int minutesFromNow)
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddMinutes(minutesFromNow));

        // Act
        ReservationResult result = classSession.Reserve(Guid.NewGuid(), _clock);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ReservationResult.ClassSessionStarted));
            Assert.That(classSession.Reservations, Is.Empty);
        });
    }

    [Test]
    public void Reserve_JustBeforeStart_IsReserved()
    {
        // Arrange
        ClassSession classSession = ClassSessionStartingAt(Now.AddSeconds(1));

        // Act
        ReservationResult result = classSession.Reserve(Guid.NewGuid(), _clock);

        // Assert
        Assert.That(result, Is.EqualTo(ReservationResult.Made));
    }
}
