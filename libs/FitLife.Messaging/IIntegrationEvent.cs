namespace FitLife.Messaging;

/// <summary>
/// An event that crosses a context boundary over RabbitMQ. Each one has a schema in <c>contracts/events/</c>
/// (docs/domain/CONTEXT-MAP.md section 5).
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Gets the routing key: the schema's file name without <c>.schema.json</c>, e.g. <c>reservation-made</c>.</summary>
    static abstract string RoutingKey { get; }
}
