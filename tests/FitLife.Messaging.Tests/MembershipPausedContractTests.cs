using System.Text;
using System.Text.Json;
using FitLife.Contracts.Testing;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The C# MembershipPaused and contracts/events/membership-paused.schema.json describe the same message (#231).</summary>
public sealed class MembershipPausedContractTests
{
    [Test]
    public void Serialize_MembershipPaused_ValidatesAgainstTheSchema()
    {
        // Act
        byte[] body = EventJson.Serialize(Example());

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
        Assert.That(read, Is.EqualTo(Example()));
    }

    [Test]
    public void Deserialize_WithoutMemberId_Throws()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes("""{"from":"2026-12-01","to":"2026-12-31"}""");

        // Act and assert
        Assert.Throws<JsonException>(() => EventJson.Deserialize<MembershipPaused>(body));
    }

    [Test]
    public void Deserialize_AMemberIdThatIsNull_Throws()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes("""{"memberId":null,"from":"2026-12-01","to":"2026-12-31"}""");

        // Act and assert
        Assert.Throws<JsonException>(() => EventJson.Deserialize<MembershipPaused>(body));
    }

    [Test]
    public void RoutingKey_IsTheSchemasFileName()
    {
        // Act
        string key = MembershipPaused.RoutingKey;

        // Assert
        Assert.That(key, Is.EqualTo("membership-paused"));
    }

    // The same values as the record's Example, written out so the test doesn't read what it checks
    private static MembershipPaused Example()
    {
        return new MembershipPaused(
        "6741f0c2a9e1b3d4c5f6071a",
        new DateOnly(2026, 12, 1),
        new DateOnly(2026, 12, 31));
    }
}
