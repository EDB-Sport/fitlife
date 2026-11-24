using System.Text;
using System.Text.Json;
using FitLife.Contracts.Testing;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The C# ReservationMade and contracts/events/reservation-made.schema.json describe the same message (#92).</summary>
public sealed class ReservationMadeContractTests
{
    private static readonly ReservationMade Event = new(
        "6741f0c2a9e1b3d4c5f60718",
        "6741f0c2a9e1b3d4c5f60719",
        "6741f0c2a9e1b3d4c5f6071a",
        new DateTimeOffset(2026, 12, 1, 18, 0, 0, TimeSpan.FromHours(1)));

    [Test]
    public void Serialize_ReservationMade_ValidatesAgainstTheSchema()
    {
        // Act
        byte[] body = EventJson.Serialize(Event);

        // Assert
        Assert.That(EventSchemas.Validate(ReservationMade.RoutingKey, Encoding.UTF8.GetString(body)), Is.Empty);
    }

    [Test]
    public void Deserialize_TheSchemasExample_ReadsEveryField()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes(EventSchemas.Example("reservation-made"));

        // Act
        ReservationMade read = EventJson.Deserialize<ReservationMade>(body);

        // Assert
        Assert.That(read, Is.EqualTo(new ReservationMade(
            "6741f0c2a9e1b3d4c5f60718",
            "6741f0c2a9e1b3d4c5f60719",
            "6741f0c2a9e1b3d4c5f6071a",
            new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero))));
    }

    [Test]
    public void Deserialize_WithoutAMemberId_Throws()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes("""{"reservationId":"r","classSessionId":"c","startTime":"2026-12-01T17:00:00+00:00"}""");

        // Act and assert
        Assert.Throws<JsonException>(() => EventJson.Deserialize<ReservationMade>(body));
    }

    [Test]
    public void RoutingKey_IsTheSchemasFileName()
    {
        // Act
        string key = ReservationMade.RoutingKey;

        // Assert
        Assert.That(key, Is.EqualTo("reservation-made"));
    }
}
