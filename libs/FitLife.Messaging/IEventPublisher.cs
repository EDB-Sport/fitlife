namespace FitLife.Messaging;

/// <summary>Publishes events to the other contexts. Register it with <c>AddEventPublisher()</c>.</summary>
public interface IEventPublisher
{
    /// <summary>Publishes an event, with the current correlation ID in its headers.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="integrationEvent">The event.</param>
    /// <param name="cancellationToken">Cancels the publish.</param>
    /// <returns>A task that completes when RabbitMQ has the message.</returns>
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent;
}
