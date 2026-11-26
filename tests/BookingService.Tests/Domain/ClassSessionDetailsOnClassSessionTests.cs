using BookingService.Domain;

namespace BookingService.Tests.Domain;

/// <summary>A class session knows its centre, class type, instructor and duration (#252).</summary>
public sealed class ClassSessionDetailsOnClassSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Constructor_WithDetails_ExposesThem()
    {
        // Arrange
        var details = new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60));

        // Act
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(20), Now.AddDays(1), details);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(classSession.Details, Is.EqualTo(details));
            Assert.That(classSession.CenterId, Is.EqualTo(details.CenterId));
            Assert.That(classSession.ClassTypeId, Is.EqualTo(details.ClassTypeId));
        });
    }
}
