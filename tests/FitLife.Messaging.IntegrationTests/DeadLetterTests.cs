using System.Globalization;
using System.Text;
using FitLife.Messaging.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Testcontainers.RabbitMq;

namespace FitLife.Messaging.IntegrationTests;

/// <summary>A message the subscriber can't handle is tried again, and then kept in a dead-letter queue, never lost (#257).</summary>
public sealed class DeadLetterTests
{
    // QueueTopology.DeliveryLimit, written out so that a changed limit fails here (#257)
    private const int DeliveryLimit = 5;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly ReservationMade Sample = new(
        "6741f0c2a9e1b3d4c5f60718",
        "6741f0c2a9e1b3d4c5f60719",
        "6741f0c2a9e1b3d4c5f6071a",
        new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero));

    // The image compose uses (deploy/compose/infrastructure.yml)
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4.4.1-management").Build();

    [OneTimeSetUp]
    public async Task StartRabbitMq()
    {
        await _rabbitMq.StartAsync().ConfigureAwait(false);
    }

    [OneTimeTearDown]
    public async Task StopRabbitMq()
    {
        await _rabbitMq.DisposeAsync().ConfigureAwait(false);
    }

    [Test]
    public async Task Consume_SubscriberFailsOnce_HandlesTheEventOnTheNextDelivery()
    {
        // Arrange
        var subscriber = new FailingSubscriber(failures: 1);
        using IHost host = await StartHostAsync(subscriber).ConfigureAwait(false);

        // Act
        await host.Services.GetRequiredService<IEventPublisher>().PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        ReservationMade handled = await subscriber.Handled.WaitAsync(Timeout).ConfigureAwait(false);
        Assert.That(handled, Is.EqualTo(Sample));
    }

    [Test]
    public async Task Consume_SubscriberAlwaysFails_PutsTheMessageInTheDeadLetterQueue()
    {
        // Arrange
        using IHost host = await StartHostAsync(new FailingSubscriber(failures: int.MaxValue)).ConfigureAwait(false);
        Task<string> deadLetter = FirstDeadLetterAsync(ConsumerOf(host).Queue);

        // Act
        await host.Services.GetRequiredService<IEventPublisher>().PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        string body = await deadLetter.WaitAsync(Timeout).ConfigureAwait(false);
        Assert.That(body, Does.Contain(Sample.ReservationId));
    }

    [Test]
    public async Task Consume_SubscriberAlwaysFails_DeliversItOnceAndThenDeliveryLimitTimesMore()
    {
        // Arrange
        var subscriber = new FailingSubscriber(failures: int.MaxValue);
        using IHost host = await StartHostAsync(subscriber).ConfigureAwait(false);
        Task<string> deadLetter = FirstDeadLetterAsync(ConsumerOf(host).Queue);

        // Act
        await host.Services.GetRequiredService<IEventPublisher>().PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);
        await deadLetter.WaitAsync(Timeout).ConfigureAwait(false);

        // Assert
        Assert.That(subscriber.Calls, Is.EqualTo(1 + DeliveryLimit));
    }

    [Test]
    public async Task Consume_ABodyThatIsNotTheEvent_PutsTheMessageInTheDeadLetterQueue()
    {
        // Arrange
        using IHost host = await StartHostAsync(new FailingSubscriber(failures: 0)).ConfigureAwait(false);
        Task<string> deadLetter = FirstDeadLetterAsync(ConsumerOf(host).Queue);

        // Act
        await PublishRawAsync("not json").ConfigureAwait(false);

        // Assert
        string body = await deadLetter.WaitAsync(Timeout).ConfigureAwait(false);
        Assert.That(body, Is.EqualTo("not json"));
    }

    private async Task<IHost> StartHostAsync(FailingSubscriber subscriber)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        var broker = new Uri(_rabbitMq.GetConnectionString());
        string[] credentials = broker.UserInfo.Split(':');
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["RabbitMq:HostName"] = broker.Host,
            ["RabbitMq:Port"] = broker.Port.ToString(CultureInfo.InvariantCulture),
            ["RabbitMq:UserName"] = credentials[0],
            ["RabbitMq:Password"] = credentials[1],
        });
        builder.Services.AddRabbitMq(builder.Configuration);
        builder.Services.AddEventPublisher();
        builder.Services.AddSingleton<IEventSubscriber<ReservationMade>>(subscriber);
        builder.Services.AddEventConsumer<ReservationMade>("test-" + Guid.NewGuid().ToString("N"));
        IHost host = builder.Build();
        await host.StartAsync().ConfigureAwait(false);
        await ConsumerOf(host).Consuming.WaitAsync(Timeout).ConfigureAwait(false);
        return host;
    }

    private static RabbitMqEventConsumer<ReservationMade> ConsumerOf(IHost host)
    {
        return host.Services.GetServices<IHostedService>().OfType<RabbitMqEventConsumer<ReservationMade>>().Single();
    }

    /// <summary>The body of the first message that reaches the queue's dead-letter queue.</summary>
    private async Task<string> FirstDeadLetterAsync(string queue)
    {
        var first = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new ConnectionFactory { Uri = new Uri(_rabbitMq.GetConnectionString()) };
        IConnection connection = await factory.CreateConnectionAsync().ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            IChannel channel = await connection.CreateChannelAsync().ConfigureAwait(false);
            await using (channel.ConfigureAwait(false))
            {
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, delivery) =>
                {
                    first.TrySetResult(Encoding.UTF8.GetString(delivery.Body.Span));
                    return Task.CompletedTask;
                };
                await channel.BasicConsumeAsync(queue + ".dead-letter", autoAck: true, consumer).ConfigureAwait(false);
                return await first.Task.WaitAsync(Timeout).ConfigureAwait(false);
            }
        }
    }

    private async Task PublishRawAsync(string body)
    {
        var factory = new ConnectionFactory { Uri = new Uri(_rabbitMq.GetConnectionString()) };
        IConnection connection = await factory.CreateConnectionAsync().ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            IChannel channel = await connection.CreateChannelAsync().ConfigureAwait(false);
            await using (channel.ConfigureAwait(false))
            {
                await channel.BasicPublishAsync(RabbitMqEventPublisher.Exchange, ReservationMade.RoutingKey, Encoding.UTF8.GetBytes(body)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Throws for the first <c>failures</c> deliveries, then records the event.</summary>
    private sealed class FailingSubscriber(int failures) : IEventSubscriber<ReservationMade>
    {
        private readonly TaskCompletionSource<ReservationMade> _handled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public Task<ReservationMade> Handled => _handled.Task;

        public int Calls => Volatile.Read(ref _calls);

        public Task HandleAsync(ReservationMade integrationEvent, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) <= failures)
            {
                throw new InvalidOperationException("The subscriber fails on purpose.");
            }

            _handled.TrySetResult(integrationEvent);
            return Task.CompletedTask;
        }
    }
}
