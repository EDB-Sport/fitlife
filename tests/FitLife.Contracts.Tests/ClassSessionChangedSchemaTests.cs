using System.Text.Json.Nodes;
using FitLife.Contracts.Testing;

namespace FitLife.Contracts.Tests;

/// <summary>contracts/events/class-session-changed.schema.json: Hold ændret, published by Booking (#231).</summary>
public sealed class ClassSessionChangedSchemaTests
{
    private const string EventName = "class-session-changed";

    private const string Sample = """
        {
          "classSessionId": "6741f0c2a9e1b3d4c5f60719",
          "memberIds": ["6741f0c2a9e1b3d4c5f6071a"],
          "previousStartTime": "2026-12-01T17:00:00+00:00",
          "startTime": "2026-12-01T18:00:00+00:00"
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

    [TestCase("classSessionId")]
    [TestCase("memberIds")]
    [TestCase("previousStartTime")]
    [TestCase("startTime")]
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

    [TestCase("previousStartTime")]
    [TestCase("startTime")]
    public void Validate_ATimeWithoutAnOffset_IsInvalid(string field)
    {
        // Arrange
        JsonObject sample = ParseSample();
        sample[field] = "2026-12-01T17:00:00";

        // Act
        IReadOnlyList<string> errors = EventSchemas.Validate(EventName, sample.ToJsonString());

        // Assert
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public void Validate_AnEmptyClassSessionId_IsInvalid()
    {
        // Arrange
        JsonObject sample = ParseSample();
        sample["classSessionId"] = string.Empty;

        // Act
        IReadOnlyList<string> errors = EventSchemas.Validate(EventName, sample.ToJsonString());

        // Assert
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public void Validate_WithNoMemberIds_IsValid()
    {
        // Arrange: nobody had a seat, which is allowed
        JsonObject sample = ParseSample();
        sample["memberIds"] = new JsonArray();

        // Act
        IReadOnlyList<string> errors = EventSchemas.Validate(EventName, sample.ToJsonString());

        // Assert
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void Validate_MemberIdsThatAreNotStrings_IsInvalid()
    {
        // Arrange
        JsonObject sample = ParseSample();
        sample["memberIds"] = new JsonArray(1, 2);

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
