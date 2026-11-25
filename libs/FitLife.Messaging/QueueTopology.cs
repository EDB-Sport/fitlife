using RabbitMQ.Client;

namespace FitLife.Messaging;

/// <summary>
/// The exchanges and queues a consumer needs. Every declaration is idempotent, so publishers and consumers may start in
/// any order. A queue's arguments can't change once it exists (RabbitMQ refuses with PRECONDITION_FAILED).
/// </summary>
internal static class QueueTopology
{
    /// <summary>Where a message goes when it can't be handled (#257).</summary>
    public const string DeadLetterExchange = "fitlife.events.dead-letter";

    /// <summary>How many times a message is redelivered after its first delivery before it is dead-lettered (#257).</summary>
    public const int DeliveryLimit = 5;

    /// <summary>Declares the events exchange, the consumer's queue bound to it, and the queue's dead-letter queue.</summary>
    /// <param name="channel">An open channel.</param>
    /// <param name="queue">The consumer's queue, e.g. <c>notification.reservation-made</c>.</param>
    /// <param name="routingKey">The event's routing key.</param>
    /// <param name="cancellationToken">Cancels the declarations.</param>
    /// <returns>A task that completes when everything exists.</returns>
    public static async Task DeclareAsync(IChannel channel, string queue, string routingKey, CancellationToken cancellationToken)
    {
        await RabbitMqEventPublisher.DeclareExchangeAsync(channel, cancellationToken).ConfigureAwait(false);

        // <queue>.dead-letter keeps the message until someone looks (contracts/events/README.md)
        string deadLetterQueue = queue + ".dead-letter";
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.QueueDeclareAsync(deadLetterQueue, durable: true, exclusive: false, autoDelete: false, Quorum(), cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.QueueBindAsync(deadLetterQueue, DeadLetterExchange, queue, cancellationToken: cancellationToken).ConfigureAwait(false);

        // A quorum queue counts deliveries: a requeued message is tried once plus DeliveryLimit times, then dead-lettered
        Dictionary<string, object?> arguments = Quorum();
        arguments["x-delivery-limit"] = DeliveryLimit;
        arguments["x-dead-letter-exchange"] = DeadLetterExchange;
        arguments["x-dead-letter-routing-key"] = queue;
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.QueueBindAsync(queue, RabbitMqEventPublisher.Exchange, routingKey, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> Quorum()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["x-queue-type"] = "quorum" };
    }
}
