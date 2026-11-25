using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.Tests.Domain;

/// <summary>The free seats a <see cref="ClassSession"/> shows in the class schedule (#171).</summary>
public sealed class ClassSessionFreeSeatsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ClassSessionDetails Details = new(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60));
    private readonly FakeTimeProvider _clock = new(Now);

    private static ClassSession ClassSessionWithCapacity(int capacity)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(capacity), Now.AddDays(1), Details);
    }

    [Test]
    public void FreeSeats_AreTheSeatsNotReservedYet()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(2);

        // Act
        classSession.Reserve(Guid.NewGuid(), _clock);

        // Assert
        Assert.That(classSession.FreeSeats, Is.EqualTo(1));
    }

    [Test]
    public void FreeSeats_OfACancelledClassSession_AreZero()
    {
        // Arrange
        ClassSession classSession = ClassSessionWithCapacity(2);

        // Act
        classSession.Cancel();

        // Assert
        Assert.That(classSession.FreeSeats, Is.Zero);
    }
}
