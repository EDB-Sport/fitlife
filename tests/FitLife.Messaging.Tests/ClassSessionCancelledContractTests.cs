using System.Text;
using System.Text.Json;
using FitLife.Contracts.Testing;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The C# ClassSessionCancelled and contracts/events/class-session-cancelled.schema.json describe the same message (#231).</summary>
public sealed class ClassSessionCancelledContractTests
{
    [Test]
    public void Serialize_ClassSessionCancelled_ValidatesAgainstTheSchema()
    {
        // Act
        byte[] body = EventJson.Serialize(Example());

        // Assert
        Assert.That(EventSchemas.Validate(ClassSessionCancelled.RoutingKey, Encoding.UTF8.GetString(body)), Is.Empty);
    }

    [Test]
    public void Deserialize_TheSchemasExample_ReadsEveryField()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes(EventSchemas.Example("class-session-cancelled"));

        // Act
        ClassSessionCancelled read = EventJson.Deserialize<ClassSessionCancelled>(body);

        // Assert
        ClassSessionCancelled expected = Example();
        Assert.Multiple(() =>
        {
            Assert.That(read.ClassSessionId, Is.EqualTo(expected.ClassSessionId));
            Assert.That(read.MemberIds, Is.EqualTo(expected.MemberIds));
            Assert.That(read.StartTime, Is.EqualTo(expected.StartTime));
        });
    }

    [Test]
    public void Deserialize_WithoutClassSessionId_Throws()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes("""{"memberIds":["6741f0c2a9e1b3d4c5f6071a", "6741f0c2a9e1b3d4c5f6071b"],"startTime":"2026-12-01T17:00:00+00:00"}""");

        // Act and assert
        Assert.Throws<JsonException>(() => EventJson.Deserialize<ClassSessionCancelled>(body));
    }

    [Test]
    public void Deserialize_AClassSessionIdThatIsNull_Throws()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes("""{"classSessionId":null,"memberIds":["6741f0c2a9e1b3d4c5f6071a", "6741f0c2a9e1b3d4c5f6071b"],"startTime":"2026-12-01T17:00:00+00:00"}""");

        // Act and assert
        Assert.Throws<JsonException>(() => EventJson.Deserialize<ClassSessionCancelled>(body));
    }

    [Test]
    public void RoutingKey_IsTheSchemasFileName()
    {
        // Act
        string key = ClassSessionCancelled.RoutingKey;

        // Assert
        Assert.That(key, Is.EqualTo("class-session-cancelled"));
    }

    // The same values as the record's Example, written out so the test doesn't read what it checks
    private static ClassSessionCancelled Example()
    {
        return new ClassSessionCancelled(
        "6741f0c2a9e1b3d4c5f60719",
        ["6741f0c2a9e1b3d4c5f6071a", "6741f0c2a9e1b3d4c5f6071b"],
        new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero));
    }
}
