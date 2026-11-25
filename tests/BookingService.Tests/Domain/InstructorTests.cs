using BookingService.Domain;

namespace BookingService.Tests.Domain;

/// <summary>Instruktør: the trainer who leads a class session, known to Booking by name (#252).</summary>
public sealed class InstructorTests
{
    [TestCase("")]
    [TestCase("   ")]
    public void Instructor_WithABlankName_IsRejected(string name)
    {
        // Arrange
        // (the blank name comes from the test case)

        // Act
        TestDelegate create = () => _ = new Instructor(name);

        // Assert
        Assert.That(create, Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void Instructor_WithAName_KeepsIt()
    {
        // Arrange
        const string name = "Mette";

        // Act
        var instructor = new Instructor(name);

        // Assert
        Assert.That(instructor.Name, Is.EqualTo(name));
    }
}
