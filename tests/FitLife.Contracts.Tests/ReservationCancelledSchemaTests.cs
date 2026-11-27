using System.Text.Json.Nodes;
using FitLife.Contracts.Testing;

namespace FitLife.Contracts.Tests;

/// <summary>contracts/events/reservation-cancelled.schema.json (#231). A change to the schema fails here until the sample below changes with it.</summary>
public sealed class ReservationCancelledSchemaTests
{
    private const string EventName = "reservation-cancelled";

    private const string Sample = """
        {
          "reservationId": "6741f0c2a9e1b3d4c5f60718",
          "classSessionId": "6741f0c2a9e1b3d4c5f60719",
          "memberId": "6741f0c2a9e1b3d4c5f6071a",
          "startTime": "2026-12-01T17:00:00+00:00",
          "isLateCancellation": true
        }
        """;

    [Test]
    public void Validate_TheSample_IsValid()
    {
        // Act
        IReadOnlyList<string> errors = EventSchemas.Validate(EventName, Sample);

        // Assert
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void Validate_TheSchemasOwnExample_IsValid()
    {
        // Arrange
        string example = EventSchemas.Example(EventName);

        // Act
        IReadOnlyList<string> errors = EventSchemas.Validate(EventName, example);

        // Assert
        Assert.That(errors, Is.Empty);
    }

    [TestCase("reservationId")]
    [TestCase("classSessionId")]
    [TestCase("memberId")]
    [TestCase("startTime")]
    [TestCase("isLateCancellation")]
    public void Validate_WithoutARequiredField_IsInvalid(string field)
    {
        // Arrange
        JsonObject sample = ParseSample();
        sample.Remove(field);

        // Act
        IReadOnlyList<string> errors = EventSchemas.Validate(EventName, sample.ToJsonString());

        // Assert
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public void Validate_WithAFieldTheSchemaDoesNotName_IsInvalid()
    {
        // Arrange
        JsonObject sample = ParseSample();
        sample["bookingId"] = "6741f0c2a9e1b3d4c5f60718";

        // Act
        IReadOnlyList<string> errors = EventSchemas.Validate(EventName, sample.ToJsonString());

        // Assert
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public void Validate_ReservationIdEmpty_IsInvalid()
    {
        // Arrange
        JsonObject sample = ParseSample();
        sample["reservationId"] = "";

        // Act
        IReadOnlyList<string> errors = EventSchemas.Validate(EventName, sample.ToJsonString());

        // Assert
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public void Validate_StartTimeWithoutAnOffset_IsInvalid()
    {
        // Arrange
        JsonObject sample = ParseSample();
        sample["startTime"] = "2026-12-01T17:00:00";

        // Act
        IReadOnlyList<string> errors = EventSchemas.Validate(EventName, sample.ToJsonString());

        // Assert
        Assert.That(errors, Is.Not.Empty);
    }

    private static JsonObject ParseSample()
    {
        return JsonNode.Parse(Sample)?.AsObject() ?? throw new InvalidOperationException("The sample is not a JSON object.");
    }
}
