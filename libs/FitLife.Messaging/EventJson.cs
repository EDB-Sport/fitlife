using System.Text.Json;

namespace FitLife.Messaging;

/// <summary>The JSON format of an event body: camelCase, as in the schemas in <c>contracts/events/</c>.</summary>
public static class EventJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // A missing or null field is an error, not a null in a non-nullable property
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    /// <summary>Serialises an event to the bytes of a message body.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="integrationEvent">The event.</param>
    /// <returns>UTF-8 JSON.</returns>
    public static byte[] Serialize<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent
    {
        return JsonSerializer.SerializeToUtf8Bytes(integrationEvent, Options);
    }

    /// <summary>Reads an event from a message body.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="body">UTF-8 JSON.</param>
    /// <returns>The event.</returns>
    /// <exception cref="JsonException">The body is not this event.</exception>
    public static TEvent Deserialize<TEvent>(ReadOnlySpan<byte> body)
        where TEvent : IIntegrationEvent
    {
        return JsonSerializer.Deserialize<TEvent>(body, Options) ?? throw new JsonException($"The body is null, not a {typeof(TEvent).Name}.");
    }
}
