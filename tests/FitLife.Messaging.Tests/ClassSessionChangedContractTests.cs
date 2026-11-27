using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The C# ClassSessionChanged and contracts/events/class-session-changed.schema.json describe the same message (#231).</summary>
public sealed class ClassSessionChangedContractTests
{
    private static readonly string[] ExampleMemberIds = ["6741f0c2a9e1b3d4c5f6071a"];

    private static readonly ClassSessionChanged Event = new ClassSessionChanged(
        "6741f0c2a9e1b3d4c5f60719",
        ["6741f0c2a9e1b3d4c5f6071a"],
        new DateTimeOffset(2026, 12, 1, 18, 0, 0, TimeSpan.FromHours(1)),
        new DateTimeOffset(2026, 12, 1, 19, 0, 0, TimeSpan.FromHours(1)));

    [Test]
    public void Serialize_ClassSessionChanged_ValidatesAgainstTheSchema()
    {
        // Act
        byte[] body = EventJson.Serialize(Event);

        // Assert
        Assert.That(EventSchemas.Validate(ClassSessionChanged.RoutingKey, Encoding.UTF8.GetString(body)), Is.Empty);
    }

    [Test]
    public void Deserialize_TheSchemasExample_ReadsEveryField()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes(EventSchemas.Example("class-session-changed"));

        // Act
        ClassSessionChanged read = EventJson.Deserialize<ClassSessionChanged>(body);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(read.ClassSessionId, Is.EqualTo("6741f0c2a9e1b3d4c5f60719"));
            Assert.That(read.MemberIds, Is.EqualTo(ExampleMemberIds));
            Assert.That(read.PreviousStartTime, Is.EqualTo(new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero)));
            Assert.That(read.StartTime, Is.EqualTo(new DateTimeOffset(2026, 12, 1, 18, 0, 0, TimeSpan.Zero)));
        });
    }

    [Test]
    public void RoutingKey_IsTheSchemasFileName()
    {
        // Act
        string key = ClassSessionChanged.RoutingKey;

        // Assert
        Assert.That(key, Is.EqualTo("class-session-changed"));
    }
}
