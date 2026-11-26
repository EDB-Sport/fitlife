using System.Text.Json;
using System.Text.Json.Nodes;
using FitLife.Messaging.Events;

namespace FitLife.Messaging.Tests;

/// <summary>The schemas in contracts/events/ are generated from the integration event records (#491).</summary>
public sealed class EventSchemaTests
{
    [Test]
    public void Generate_ReservationMade_IsEquivalentToTheCommittedSchema()
    {
        // Arrange
        var committed = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "contracts", "events", "reservation-made.schema.json")));

        // Act
        var generated = JsonNode.Parse(EventSchema.Generate<ReservationMade>());

        // Assert
        Assert.That(JsonNode.DeepEquals(generated, committed), Is.True, generated?.ToJsonString());
    }

    [Test]
    public void Generate_ReservationMade_ForbidsFieldsTheRecordDoesNotHave()
    {
        // Act
        JsonNode schema = Parse(EventSchema.Generate<ReservationMade>());

        // Assert
        Assert.That(schema["additionalProperties"]?.GetValue<bool>(), Is.False);
    }

    [Test]
    public void Generate_ReservationMade_RequiresEveryConstructorParameter()
    {
        // Arrange
        var expected = JsonNode.Parse("""["reservationId", "classSessionId", "memberId", "startTime"]""");

        // Act
        JsonNode schema = Parse(EventSchema.Generate<ReservationMade>());

        // Assert
        Assert.That(JsonNode.DeepEquals(schema["required"], expected), Is.True);
    }

    [Test]
    public void Generate_ReservationMade_HasTheRecordsExample()
    {
        // Arrange
        var example = JsonNode.Parse(EventJson.Serialize((ReservationMade)ReservationMade.Example));

        // Act
        JsonNode schema = Parse(EventSchema.Generate<ReservationMade>());

        // Assert
        Assert.That(JsonNode.DeepEquals(schema["examples"]?[0], example), Is.True);
    }

    [Test]
    public void Generate_ReservationMade_UsesLineFeedsAndEndsWithOne()
    {
        // Act
        string schema = EventSchema.Generate<ReservationMade>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(schema, Does.Not.Contain("\r"));
            Assert.That(schema, Does.EndWith("}\n"));
        });
    }

    [Test]
    public void GenerateAll_HasEveryEventUnderItsRoutingKey()
    {
        // Act
        IReadOnlyDictionary<string, string> schemas = EventSchema.GenerateAll();

        // Assert
        Assert.That(schemas, Is.EqualTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ReservationMade.RoutingKey] = EventSchema.Generate<ReservationMade>(),
        }));
    }

    [Test]
    public void GenerateAll_HasOneSchemaPerEventRecord()
    {
        // Arrange
        int records = typeof(IIntegrationEvent).Assembly.GetTypes()
            .Count(type => type.IsAssignableTo(typeof(IIntegrationEvent)) && !type.IsInterface);

        // Act
        IReadOnlyDictionary<string, string> schemas = EventSchema.GenerateAll();

        // Assert
        Assert.That(schemas, Has.Count.EqualTo(records));
    }

    private static JsonNode Parse(string json)
    {
        return JsonNode.Parse(json) ?? throw new JsonException("The schema is null.");
    }
}
