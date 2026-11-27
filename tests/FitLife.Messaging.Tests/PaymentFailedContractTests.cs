using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The C# PaymentFailed and contracts/events/payment-failed.schema.json describe the same message (#231).</summary>
public sealed class PaymentFailedContractTests
{
    private static readonly PaymentFailed Event = new PaymentFailed("6741f0c2a9e1b3d4c5f6071a", new DateTimeOffset(2026, 12, 1, 7, 0, 0, TimeSpan.FromHours(1)));

    [Test]
    public void Serialize_PaymentFailed_ValidatesAgainstTheSchema()
    {
        // Act
        byte[] body = EventJson.Serialize(Event);

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
        Assert.That(read, Is.EqualTo(new PaymentFailed("6741f0c2a9e1b3d4c5f6071a", new DateTimeOffset(2026, 12, 1, 6, 0, 0, TimeSpan.Zero))));
    }

    [Test]
    public void RoutingKey_IsTheSchemasFileName()
    {
        // Act
        string key = PaymentFailed.RoutingKey;

        // Assert
        Assert.That(key, Is.EqualTo("payment-failed"));
    }
}
