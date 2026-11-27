using System.Text.Json;
using FitLife.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FitLife.Messaging;

/// <summary>
/// Consumes one event type from the service's own durable queue, <c>&lt;queuePrefix&gt;.&lt;routing key&gt;</c>, and hands
/// each event to the <see cref="IEventSubscriber{TEvent}"/> inside the message's correlation ID (#19).
/// It connects in the background and keeps trying while RabbitMQ is not reachable, so the service starts
/// (and the build-time OpenAPI generator runs) without a broker. <see cref="Consuming"/> completes once the queue is bound.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public sealed partial class RabbitMqEventConsumer<TEvent> : BackgroundService, IAsyncDisposable
    where TEvent : IIntegrationEvent
{
    /// <summary>How long it waits before it tries RabbitMQ again.</summary>
    private static TimeSpan RetryDelay => TimeSpan.FromSeconds(5);

    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RabbitMqEventConsumer<TEvent>> _logger;
    private readonly TimeProvider _time;
    private readonly TaskCompletionSource _consuming = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationToken _stopping;
    private IChannel? _channel;

    /// <summary>Initializes a new instance of the <see cref="RabbitMqEventConsumer{TEvent}"/> class.</summary>
    /// <param name="connection">The service's RabbitMQ connection.</param>
    /// <param name="scopes">Creates a DI scope per message for the subscriber.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="time">The clock for the wait between tries.</param>
    /// <param name="queuePrefix">The consuming service, e.g. <c>notification</c>: each service gets its own copy of every event.</param>
    public RabbitMqEventConsumer(RabbitMqConnection connection, IServiceScopeFactory scopes, ILogger<RabbitMqEventConsumer<TEvent>> logger, TimeProvider time, string queuePrefix)
    {
        _connection = connection;
        _scopes = scopes;
        _logger = logger;
        _time = time;
        Queue = $"{queuePrefix}.{TEvent.RoutingKey}";
    }

    /// <summary>Gets the name of the queue this consumer reads.</summary>
    public string Queue { get; }

    /// <summary>Gets a task that completes once the queue is bound and messages are being handed to the subscriber.</summary>
    public Task Consuming => _consuming.Task;

    /// <summary>Gets a value indicating whether the channel it consumes on is open. False while the connection to RabbitMQ is lost (#302).</summary>
    internal bool IsChannelOpen => _channel is { IsOpen: true };

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stopping = stoppingToken;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken).ConfigureAwait(false);
                _consuming.TrySetResult();
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Once connected, RabbitMQ.Client's automatic recovery (on by default) reconnects a lost connection,
                // opens the channel again, declares the queue and consumes it (ConsumerReconnectTests, #302)
                LogNotReachable(exception, Queue, RetryDelay);
                await Task.Delay(RetryDelay, _time, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync().ConfigureAwait(false);
        }

        _channel = await _connection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);
        await QueueTopology.DeclareAsync(_channel, Queue, TEvent.RoutingKey, cancellationToken).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;
        await _channel.BasicConsumeAsync(Queue, autoAck: false, consumer, cancellationToken).ConfigureAwait(false);
        LogConsuming(Queue);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        if (_channel is not { IsOpen: true })
        {
            return;
        }

        try
        {
            await _channel.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Stopped while still connecting: the cancelled set-up has already torn the channel down
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync().ConfigureAwait(false);
        }

        Dispose();
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs delivery)
    {
        IChannel channel = ((AsyncEventingBasicConsumer)sender).Channel;
        string id = CorrelationHeaders.Read(delivery.BasicProperties.Headers) ?? Guid.NewGuid().ToString();
        using (CorrelationContext.Begin(_logger, id))
        {
            TEvent integrationEvent;
            try
            {
                integrationEvent = EventJson.Deserialize<TEvent>(delivery.Body.Span);
            }
            catch (JsonException exception)
            {
                // Redelivering a body that can't be read only fails again: straight to the dead-letter queue
                LogUnreadable(exception, Queue);
                await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            await HandleAsync(channel, delivery.DeliveryTag, integrationEvent).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(IChannel channel, ulong deliveryTag, TEvent integrationEvent)
    {
        try
        {
            AsyncServiceScope scope = _scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                await scope.ServiceProvider.GetRequiredService<IEventSubscriber<TEvent>>().HandleAsync(integrationEvent, _stopping).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Requeued: RabbitMQ tries it again, and dead-letters it after the delivery limit. Reject, not nack:
            // since RabbitMQ 4.3 a nack requeue doesn't count towards the limit (rabbitmq.com/docs/nack)
            LogHandlerFailed(exception, Queue);
            await channel.BasicRejectAsync(deliveryTag, requeue: true, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        await channel.BasicAckAsync(deliveryTag, multiple: false, CancellationToken.None).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consuming {Queue}")]
    private partial void LogConsuming(string queue);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMQ is not reachable for {Queue}; trying again in {RetryDelay}")]
    private partial void LogNotReachable(Exception exception, string queue, TimeSpan retryDelay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A message on {Queue} is not the event; it goes to the dead-letter queue")]
    private partial void LogUnreadable(Exception exception, string queue);

    [LoggerMessage(Level = LogLevel.Error, Message = "The subscriber failed on {Queue}; the message is tried again, and dead-lettered after the delivery limit")]
    private partial void LogHandlerFailed(Exception exception, string queue);
}
