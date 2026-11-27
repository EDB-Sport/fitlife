using System.Text;
using System.Text.Json;
using FitLife.Contracts.Testing;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The C# MembershipCancelled and contracts/events/membership-cancelled.schema.json describe the same message (#231).</summary>
public sealed class MembershipCancelledContractTests
{
    [Test]
    public void Serialize_MembershipCancelled_ValidatesAgainstTheSchema()
    {
        // Act
        byte[] body = EventJson.Serialize(Example());

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
        Assert.That(read, Is.EqualTo(Example()));
    }

    [Test]
    public void Deserialize_WithoutMemberId_Throws()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes("""{"lastTrainingDay":"2027-01-31"}""");

        // Act and assert
        Assert.Throws<JsonException>(() => EventJson.Deserialize<MembershipCancelled>(body));
    }

    [Test]
    public void Deserialize_AMemberIdThatIsNull_Throws()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes("""{"memberId":null,"lastTrainingDay":"2027-01-31"}""");

        // Act and assert
        Assert.Throws<JsonException>(() => EventJson.Deserialize<MembershipCancelled>(body));
    }

    [Test]
    public void RoutingKey_IsTheSchemasFileName()
    {
        // Act
        string key = MembershipCancelled.RoutingKey;

        // Assert
        Assert.That(key, Is.EqualTo("membership-cancelled"));
    }

    // The same values as the record's Example, written out so the test doesn't read what it checks
    private static MembershipCancelled Example()
    {
        return new MembershipCancelled(
        "6741f0c2a9e1b3d4c5f6071a",
        new DateOnly(2027, 1, 31));
    }
}
