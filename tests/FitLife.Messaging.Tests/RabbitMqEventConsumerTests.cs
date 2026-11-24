using System.Text;
using FitLife.Correlation;
using FitLife.Messaging.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using RabbitMQ.Client;

namespace FitLife.Messaging.Tests;

/// <summary>How the consumer handles each message, on a mocked channel (#190, #19).</summary>
public sealed class RabbitMqEventConsumerTests
{
    private static readonly ReservationMade Sample = new("reservation-1", "class-session-1", "member-1", new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero));

    private static readonly ReservationMade[] Samples = [Sample];

    private static readonly string[] AbcCorrelationId = ["abc-123"];

    [Test]
    public void Queue_IsThePrefixAndTheRoutingKey()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq, new RecordingSubscriber());

        // Act
        string queue = consumer.Queue;

        // Assert
        Assert.That(queue, Is.EqualTo("notification.reservation-made"));
    }

    [Test]
    public async Task StartAsync_DeclaresADurableQueueBoundToTheEventsExchange()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq, new RecordingSubscriber());

        // Act
        await StartAsync(consumer).ConfigureAwait(false);

        // Assert
        rabbitMq.Channel.Verify(channel => channel.QueueDeclareAsync("notification.reservation-made", true, false, false, It.IsAny<IDictionary<string, object?>?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));
        rabbitMq.Channel.Verify(channel => channel.QueueBindAsync("notification.reservation-made", "fitlife.events", "reservation-made", It.IsAny<IDictionary<string, object?>?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task StartAsync_ConsumesWithManualAcknowledgements()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq, new RecordingSubscriber());

        // Act
        await StartAsync(consumer).ConfigureAwait(false);

        // Assert
        rabbitMq.Channel.Verify(channel => channel.BasicConsumeAsync("notification.reservation-made", false, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object?>?>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Deliver_TheEvent_HandsItToTheSubscriberAndAcknowledgesIt()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        var subscriber = new RecordingSubscriber();
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq, subscriber);
        await StartAsync(consumer).ConfigureAwait(false);

        // Act
        await rabbitMq.DeliverAsync(7, EventJson.Serialize(Sample)).ConfigureAwait(false);

        // Assert
        Assert.That(subscriber.Events, Is.EqualTo(Samples));
        rabbitMq.Channel.Verify(channel => channel.BasicAckAsync(7, false, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Deliver_WithACorrelationIdHeader_TheSubscriberRunsWithThatId()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        var subscriber = new RecordingSubscriber();
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq, subscriber);
        await StartAsync(consumer).ConfigureAwait(false);
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal) { [CorrelationContext.HeaderName] = Encoding.UTF8.GetBytes("abc-123") };

        // Act
        await rabbitMq.DeliverAsync(7, EventJson.Serialize(Sample), headers).ConfigureAwait(false);

        // Assert
        Assert.That(subscriber.CorrelationIds, Is.EqualTo(AbcCorrelationId));
    }

    [Test]
    public async Task Deliver_WithoutACorrelationId_TheSubscriberRunsWithANewOne()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        var subscriber = new RecordingSubscriber();
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq, subscriber);
        await StartAsync(consumer).ConfigureAwait(false);

        // Act
        await rabbitMq.DeliverAsync(7, EventJson.Serialize(Sample)).ConfigureAwait(false);

        // Assert
        Assert.That(Guid.TryParse(subscriber.CorrelationIds.Single(), out _), Is.True);
    }

    [Test]
    public async Task Deliver_ABodyThatIsNotTheEvent_RejectsItWithoutRequeueAndSkipsTheSubscriber()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        var subscriber = new RecordingSubscriber();
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq, subscriber);
        await StartAsync(consumer).ConfigureAwait(false);

        // Act
        await rabbitMq.DeliverAsync(7, Encoding.UTF8.GetBytes("not json")).ConfigureAwait(false);

        // Assert
        Assert.That(subscriber.Events, Is.Empty);
        rabbitMq.Channel.Verify(channel => channel.BasicRejectAsync(7, false, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Deliver_SubscriberThrows_DoesNotAcknowledgeIt()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq, new RecordingSubscriber(throws: true));
        await StartAsync(consumer).ConfigureAwait(false);

        // Act
        await rabbitMq.DeliverAsync(7, EventJson.Serialize(Sample)).ConfigureAwait(false);

        // Assert
        rabbitMq.Channel.Verify(channel => channel.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task StartAsync_RabbitMqNotReachable_TriesAgainAfterTheDelay()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq { Failures = 1 };
        var time = new FakeTimeProvider();
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq, new RecordingSubscriber(), time);
        await consumer.StartAsync(CancellationToken.None).ConfigureAwait(false);

        // Act: ExecuteAsync runs on the thread pool, so keep the clock moving until the second try has bound the queue
        for (int step = 0; step < 1000 && !consumer.Consuming.IsCompleted; step++)
        {
            time.Advance(TimeSpan.FromSeconds(5));
            await Task.Yield();
        }

        // Assert
        await consumer.Consuming.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Assert.That(rabbitMq.Attempts, Is.EqualTo(2));
    }

    [Test]
    public async Task StartAsync_RabbitMqNotReachable_IsNotConsumingBeforeTheDelay()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq { Failures = int.MaxValue };
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq, new RecordingSubscriber(), new FakeTimeProvider());

        // Act
        await consumer.StartAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(consumer.Consuming.IsCompleted, Is.False);
    }

    [Test]
    public async Task StopAsync_Consuming_ClosesTheChannel()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq, new RecordingSubscriber());
        await StartAsync(consumer).ConfigureAwait(false);

        // Act
        await consumer.StopAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        rabbitMq.Channel.Verify(channel => channel.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));
    }

    private static RabbitMqEventConsumer<ReservationMade> Create(FakeRabbitMq rabbitMq, RecordingSubscriber subscriber)
    {
        return Create(rabbitMq, subscriber, new FakeTimeProvider());
    }

    private static RabbitMqEventConsumer<ReservationMade> Create(FakeRabbitMq rabbitMq, RecordingSubscriber subscriber, TimeProvider time)
    {
        RabbitMqConnection connection = rabbitMq.Client;
        ServiceProvider services = new ServiceCollection().AddSingleton<IEventSubscriber<ReservationMade>>(subscriber).BuildServiceProvider();
        return new RabbitMqEventConsumer<ReservationMade>(connection, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<RabbitMqEventConsumer<ReservationMade>>.Instance, time, "notification");
    }

    private static async Task StartAsync(RabbitMqEventConsumer<ReservationMade> consumer)
    {
        await consumer.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await consumer.Consuming.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
    }

    private sealed class RecordingSubscriber(bool throws = false) : IEventSubscriber<ReservationMade>
    {
        public List<ReservationMade> Events { get; } = [];

        public List<string?> CorrelationIds { get; } = [];

        public Task HandleAsync(ReservationMade integrationEvent, CancellationToken cancellationToken)
        {
            if (throws)
            {
                throw new InvalidOperationException("The subscriber fails on purpose.");
            }

            Events.Add(integrationEvent);
            CorrelationIds.Add(CorrelationContext.Current);
            return Task.CompletedTask;
        }
    }
}
