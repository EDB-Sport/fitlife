using System.Text;
using System.Text.Json;
using FitLife.Contracts.Testing;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The C# PaymentFailed and contracts/events/payment-failed.schema.json describe the same message (#231).</summary>
public sealed class PaymentFailedContractTests
{
    [Test]
    public void Serialize_PaymentFailed_ValidatesAgainstTheSchema()
    {
        // Act
        byte[] body = EventJson.Serialize(Example());

        // Assert
        Assert.That(EventSchemas.Validate(PaymentFailed.RoutingKey, Encoding.UTF8.GetString(body)), Is.Empty);
    }

    [Test]
    public void Deserialize_TheSchemasExample_ReadsEveryField()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes(EventSchemas.Example("payment-failed"));

        // Act
        PaymentFailed read = EventJson.Deserialize<PaymentFailed>(body);

        // Assert
        Assert.That(read, Is.EqualTo(Example()));
    }

    [Test]
    public void Deserialize_WithoutMemberId_Throws()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes("""{"failedAt":"2026-11-24T09:00:00+00:00"}""");

        // Act and assert
        Assert.Throws<JsonException>(() => EventJson.Deserialize<PaymentFailed>(body));
    }

    [Test]
    public void Deserialize_AMemberIdThatIsNull_Throws()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes("""{"memberId":null,"failedAt":"2026-11-24T09:00:00+00:00"}""");

        // Act and assert
        Assert.Throws<JsonException>(() => EventJson.Deserialize<PaymentFailed>(body));
    }

    [Test]
    public void RoutingKey_IsTheSchemasFileName()
    {
        // Act
        string key = PaymentFailed.RoutingKey;

        // Assert
        Assert.That(key, Is.EqualTo("payment-failed"));
    }

    // The same values as the record's Example, written out so the test doesn't read what it checks
    private static PaymentFailed Example()
    {
        return new PaymentFailed(
        "6741f0c2a9e1b3d4c5f6071a",
        new DateTimeOffset(2026, 11, 24, 9, 0, 0, TimeSpan.Zero));
    }
}
