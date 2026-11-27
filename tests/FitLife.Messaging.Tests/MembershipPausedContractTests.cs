using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The C# MembershipPaused and contracts/events/membership-paused.schema.json describe the same message (#231).</summary>
public sealed class MembershipPausedContractTests
{
    private static readonly MembershipPaused Event = new MembershipPaused("6741f0c2a9e1b3d4c5f6071a", new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31));

    [Test]
    public void Serialize_MembershipPaused_ValidatesAgainstTheSchema()
    {
        // Act
        byte[] body = EventJson.Serialize(Event);

        // Assert
        Assert.That(EventSchemas.Validate(MembershipPaused.RoutingKey, Encoding.UTF8.GetString(body)), Is.Empty);
    }

    [Test]
    public void Deserialize_TheSchemasExample_ReadsEveryField()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes(EventSchemas.Example("membership-paused"));

        // Act
        MembershipPaused read = EventJson.Deserialize<MembershipPaused>(body);

        // Assert
        Assert.That(read, Is.EqualTo(new MembershipPaused("6741f0c2a9e1b3d4c5f6071a", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31))));
    }

    [Test]
    public void RoutingKey_IsTheSchemasFileName()
    {
        // Act
        string key = MembershipPaused.RoutingKey;

        // Assert
        Assert.That(key, Is.EqualTo("membership-paused"));
    }
}
