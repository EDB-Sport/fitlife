using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The C# ClassSessionCancelled and contracts/events/class-session-cancelled.schema.json describe the same message (#231).</summary>
public sealed class ClassSessionCancelledContractTests
{
    private static readonly ClassSessionCancelled Event = new ClassSessionCancelled(
        "6741f0c2a9e1b3d4c5f60719",
        ["6741f0c2a9e1b3d4c5f6071a"],
        new DateTimeOffset(2026, 12, 1, 18, 0, 0, TimeSpan.FromHours(1)));

    [Test]
    public void Serialize_ClassSessionCancelled_ValidatesAgainstTheSchema()
    {
        // Act
        byte[] body = EventJson.Serialize(Event);

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
        Assert.That((read.ClassSessionId, read.MemberIds, read.StartTime), Is.EqualTo((
            "6741f0c2a9e1b3d4c5f60719",
            new[] { "6741f0c2a9e1b3d4c5f6071a", "6741f0c2a9e1b3d4c5f6071b" },
            new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero))));
    }

    [Test]
    public void RoutingKey_IsTheSchemasFileName()
    {
        // Act
        string key = ClassSessionCancelled.RoutingKey;

        // Assert
        Assert.That(key, Is.EqualTo("class-session-cancelled"));
    }
}
