using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The C# MembershipCancelled and contracts/events/membership-cancelled.schema.json describe the same message (#231).</summary>
public sealed class MembershipCancelledContractTests
{
    private static readonly MembershipCancelled Event = new MembershipCancelled("6741f0c2a9e1b3d4c5f6071a", new DateOnly(2027, 2, 28));

    [Test]
    public void Serialize_MembershipCancelled_ValidatesAgainstTheSchema()
    {
        // Act
        byte[] body = EventJson.Serialize(Event);

        // Assert
        Assert.That(EventSchemas.Validate(MembershipCancelled.RoutingKey, Encoding.UTF8.GetString(body)), Is.Empty);
    }

    [Test]
    public void Deserialize_TheSchemasExample_ReadsEveryField()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes(EventSchemas.Example("membership-cancelled"));

        // Act
        MembershipCancelled read = EventJson.Deserialize<MembershipCancelled>(body);

        // Assert
        Assert.That(read, Is.EqualTo(new MembershipCancelled("6741f0c2a9e1b3d4c5f6071a", new DateOnly(2027, 1, 31))));
    }

    [Test]
    public void RoutingKey_IsTheSchemasFileName()
    {
        // Act
        string key = MembershipCancelled.RoutingKey;

        // Assert
        Assert.That(key, Is.EqualTo("membership-cancelled"));
    }
}
