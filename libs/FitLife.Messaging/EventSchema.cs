using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using FitLife.Messaging.Events;

namespace FitLife.Messaging;

/// <summary>
/// Generates the JSON Schemas in <c>contracts/events/</c> from the integration event records (#491), with the
/// serializer options of the wire format (<see cref="EventJson"/>). <c>tools/EventSchemaGenerator</c> writes them on build.
/// </summary>
public static class EventSchema
{
    private const string Draft = "https://json-schema.org/draft/2020-12/schema";

    private static readonly JsonSerializerOptions SchemaOptions = new(EventJson.Options)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        // Keeps "+00:00" and the Danish letters readable in the committed file
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSchemaExporterOptions ExporterOptions = new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = Transform,
    };

    /// <summary>The schema of one event.</summary>
    /// <typeparam name="TEvent">The event record.</typeparam>
    /// <returns>The schema file's text, with line feeds.</returns>
    public static string Generate<TEvent>()
        where TEvent : IIntegrationEvent
    {
        var schema = new JsonObject
        {
            ["$schema"] = Draft,
            ["title"] = typeof(TEvent).Name,
            ["description"] = DescriptionOf(typeof(TEvent)),
        };
        JsonObject generated = JsonSchemaExporter.GetJsonSchemaAsNode(SchemaOptions, typeof(TEvent), ExporterOptions).AsObject();
        MoveInto(schema, generated);
        schema["examples"] = new JsonArray(JsonSerializer.SerializeToNode(TEvent.Example, typeof(TEvent), EventJson.Options));
        return schema.ToJsonString(WriteOptions) + "\n";
    }

    /// <summary>The schema of every integration event. A new event record gets a line here.</summary>
    /// <returns>The schema file's text by routing key (the file name without <c>.schema.json</c>).</returns>
    public static IReadOnlyDictionary<string, string> GenerateAll()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ReservationMade.RoutingKey] = Generate<ReservationMade>(),
            [ReservationCancelled.RoutingKey] = Generate<ReservationCancelled>(),
            [ClassSessionCancelled.RoutingKey] = Generate<ClassSessionCancelled>(),
            [ClassSessionChanged.RoutingKey] = Generate<ClassSessionChanged>(),
            [MembershipPaused.RoutingKey] = Generate<MembershipPaused>(),
            [MembershipCancelled.RoutingKey] = Generate<MembershipCancelled>(),
            [PaymentFailed.RoutingKey] = Generate<PaymentFailed>(),
        };
    }

    private static JsonNode Transform(JsonSchemaExporterContext context, JsonNode node)
    {
        if (node is not JsonObject generated)
        {
            return node;
        }

        // A field the record doesn't have is an error, so a new field is a schema change consumers see
        if (context.TypeInfo.Kind == JsonTypeInfoKind.Object)
        {
            generated["additionalProperties"] = false;
        }

        ICustomAttributeProvider? property = context.PropertyInfo?.AttributeProvider;
        if (property is null)
        {
            return generated;
        }

        var described = new JsonObject { ["description"] = DescriptionOf(property) };
        MoveInto(described, generated);
        MinLengthAttribute? minLength = property.GetCustomAttributes(typeof(MinLengthAttribute), inherit: false).OfType<MinLengthAttribute>().FirstOrDefault();
        if (minLength is not null && described["items"] is JsonObject items)
        {
            // On a list of ids, each id has the minimum length; the list itself may be empty (#231)
            items["minLength"] = minLength.Length;
        }
        else if (minLength is not null)
        {
            described["minLength"] = minLength.Length;
        }

        return described;
    }

    private static string? DescriptionOf(ICustomAttributeProvider member)
    {
        return member.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false).OfType<DescriptionAttribute>().FirstOrDefault()?.Description;
    }

    private static void MoveInto(JsonObject target, JsonObject source)
    {
        foreach (string name in source.Select(field => field.Key).ToList())
        {
            JsonNode? value = source[name];
            source.Remove(name);
            target[name] = value;
        }
    }
}
