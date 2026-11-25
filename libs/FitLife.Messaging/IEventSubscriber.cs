namespace FitLife.Messaging;

/// <summary>What a consuming service does with one event. Register it in DI; the consumer resolves it per message.</summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IEventSubscriber<in TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>Handles one event. The message is acknowledged when this completes, and dropped if it throws.</summary>
    /// <param name="integrationEvent">The event.</param>
    /// <param name="cancellationToken">Cancelled when the service stops.</param>
    /// <returns>A task that completes when the event has been handled.</returns>
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
