using FitLife.Correlation;
using RabbitMQ.Client;

namespace FitLife.Messaging;

/// <summary>Publishes events to the <see cref="Exchange"/> topic exchange, with the event's routing key.</summary>
/// <param name="connection">The service's RabbitMQ connection.</param>
public sealed class RabbitMqEventPublisher(RabbitMqConnection connection) : IEventPublisher
{
    /// <summary>The one durable topic exchange every event goes to.</summary>
    public const string Exchange = "fitlife.events";

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        // Stryker disable once Boolean : ConfigureAwait(false) -> true is an equivalent mutant (#270)
        IChannel channel = await connection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);
        // Stryker disable once Boolean : ConfigureAwait(false) -> true is an equivalent mutant (#270)
        await using (channel.ConfigureAwait(false))
        {
            // Stryker disable once Boolean : ConfigureAwait(false) -> true is an equivalent mutant (#270)
            await PublishAsync(channel, integrationEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task PublishAsync<TEvent>(IChannel channel, TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        // Stryker disable once Boolean : ConfigureAwait(false) -> true is an equivalent mutant (#270)
        await DeclareExchangeAsync(channel, cancellationToken).ConfigureAwait(false);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString(),
            Type = TEvent.RoutingKey,
            // Null by default in RabbitMQ.Client 7
            Headers = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
        CorrelationHeaders.Write(properties.Headers);

        // Stryker disable once Boolean : ConfigureAwait(false) -> true is an equivalent mutant (#270)
        await channel.BasicPublishAsync(Exchange, TEvent.RoutingKey, mandatory: false, properties, EventJson.Serialize(integrationEvent), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Declares the exchange. Idempotent, so publishers and consumers both call it and either may start first.</summary>
    /// <param name="channel">An open channel.</param>
    /// <param name="cancellationToken">Cancels the declaration.</param>
    /// <returns>A task that completes when the exchange exists.</returns>
    internal static Task DeclareExchangeAsync(IChannel channel, CancellationToken cancellationToken)
    {
        return channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
    }
}
