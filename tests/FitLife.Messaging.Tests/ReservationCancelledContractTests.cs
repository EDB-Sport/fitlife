using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The C# ReservationCancelled and contracts/events/reservation-cancelled.schema.json describe the same message (#231).</summary>
public sealed class ReservationCancelledContractTests
{
    private static readonly ReservationCancelled Event = new ReservationCancelled(
        "6741f0c2a9e1b3d4c5f60718",
        "6741f0c2a9e1b3d4c5f60719",
        "6741f0c2a9e1b3d4c5f6071a",
        new DateTimeOffset(2026, 12, 1, 18, 0, 0, TimeSpan.FromHours(1)),
        IsLateCancellation: true);

    [Test]
    public void Serialize_ReservationCancelled_ValidatesAgainstTheSchema()
    {
        // Act
        byte[] body = EventJson.Serialize(Event);

        // Assert
        Assert.That(EventSchemas.Validate(ReservationCancelled.RoutingKey, Encoding.UTF8.GetString(body)), Is.Empty);
    }

    [Test]
    public void Deserialize_TheSchemasExample_ReadsEveryField()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes(EventSchemas.Example("reservation-cancelled"));

        // Act
        ReservationCancelled read = EventJson.Deserialize<ReservationCancelled>(body);

        // Assert
        Assert.That(read, Is.EqualTo(new ReservationCancelled(
            "6741f0c2a9e1b3d4c5f60718",
            "6741f0c2a9e1b3d4c5f60719",
            "6741f0c2a9e1b3d4c5f6071a",
            new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero),
            IsLateCancellation: false)));
    }

    [Test]
    public void RoutingKey_IsTheSchemasFileName()
    {
        // Act
        string key = ReservationCancelled.RoutingKey;

        // Assert
        Assert.That(key, Is.EqualTo("reservation-cancelled"));
    }
}
