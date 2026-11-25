using BookingService.Domain;

namespace BookingService.Tests.Domain;

/// <summary>Holdoplysninger: a class session's centre, class type, instructor and duration (#252).</summary>
public sealed class ClassSessionDetailsTests
{
    private static readonly Guid CenterId = new("6f1c2d1e-0000-4000-8000-000000000001");
    private static readonly Guid ClassTypeId = new("7a2b3c4d-0000-4000-8000-000000000001");
    private static readonly Instructor Instructor = new("Mette");

    [TestCase(0)]
    [TestCase(-30)]
    public void ClassSessionDetails_WithADurationOfZeroOrLess_IsRejected(int minutes)
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(minutes);

        // Act
        Action create = () => _ = new ClassSessionDetails(CenterId, ClassTypeId, Instructor, duration);

        // Assert
        Assert.That(create, Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ClassSessionDetails_WithAPositiveDuration_IsCreated()
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(1);

        // Act
        var details = new ClassSessionDetails(CenterId, ClassTypeId, Instructor, duration);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(details.CenterId, Is.EqualTo(CenterId));
            Assert.That(details.ClassTypeId, Is.EqualTo(ClassTypeId));
            Assert.That(details.Instructor, Is.EqualTo(Instructor));
            Assert.That(details.Duration, Is.EqualTo(duration));
        });
    }
}
