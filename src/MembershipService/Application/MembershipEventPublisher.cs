using FitLife.Messaging;

namespace MembershipService.Application;

/// <summary>
/// Publishes an integration event after its change is saved (#251). A failed publish is logged and the change is kept, so the
/// member's answer is the same (#192's safe default; no outbox, so the event is lost).
/// </summary>
/// <param name="events">The RabbitMQ publisher.</param>
/// <param name="logger">Logs a publish that failed.</param>
public sealed partial class MembershipEventPublisher(IEventPublisher events, ILogger<MembershipEventPublisher> logger)
{
    /// <summary>Publishes the event; a failure is logged, never thrown.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="integrationEvent">The event.</param>
    /// <returns>A task that completes when the event is published or the failure is logged.</returns>
    public async Task PublishAsync<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent
    {
        try
        {
            // Not the request's token: the change is saved, so a caller that goes away doesn't stop its event either
            await events.PublishAsync(integrationEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException) // none of RabbitMQ's errors may undo the saved change
        {
            LogPublishFailed(TEvent.RoutingKey, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The change is saved, but {RoutingKey} wasn't published")]
    private partial void LogPublishFailed(string routingKey, Exception exception);
}
