using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace FitLife.Contracts.Testing;

/// <summary>
/// The RabbitMQ event schemas in <c>contracts/events/</c>, for producer and consumer tests (#92).
/// An event is named by its file name without <c>.schema.json</c>, e.g. <c>reservation-made</c>.
/// </summary>
public static class EventSchemas
{
    private static readonly EvaluationOptions Options = new()
    {
        OutputFormat = OutputFormat.List,
        // "format": "date-time" is only an annotation unless this is set
        RequireFormatValidation = true,
    };

    /// <summary>Validates <paramref name="json"/> against the event's schema.</summary>
    /// <param name="eventName">The schema's file name without <c>.schema.json</c>.</param>
    /// <param name="json">The event as it goes on the wire.</param>
    /// <returns>One line per error; empty when the event is valid.</returns>
    public static IReadOnlyList<string> Validate(string eventName, string json)
    {
        using var document = JsonDocument.Parse(json);
        EvaluationResults results = Load(eventName).Evaluate(document.RootElement, Options);
        if (results.IsValid)
        {
            return [];
        }

        return [.. (results.Details ?? [])
            .SelectMany(detail => (detail.Errors ?? new Dictionary<string, string>(StringComparer.Ordinal))
                .Select(error => $"{detail.InstanceLocation}: {error.Value}"))];
    }

    /// <summary>The first entry of the schema's <c>examples</c>: a sample payload for consumer tests.</summary>
    /// <param name="eventName">The schema's file name without <c>.schema.json</c>.</param>
    /// <returns>The sample, serialised as JSON.</returns>
    public static string Example(string eventName)
    {
        var schema = JsonNode.Parse(File.ReadAllText(PathOf(eventName)));
        JsonNode example = schema?["examples"]?[0] ?? throw new InvalidOperationException($"{eventName}.schema.json has no examples.");
        return example.ToJsonString();
    }

    private static JsonSchema Load(string eventName)
    {
        return JsonSchema.FromText(File.ReadAllText(PathOf(eventName)));
    }

    private static string PathOf(string eventName)
    {
        return Path.Combine(AppContext.BaseDirectory, "contracts", "events", eventName + ".schema.json");
    }
}
