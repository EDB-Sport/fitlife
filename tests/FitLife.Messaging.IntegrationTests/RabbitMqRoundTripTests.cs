using System.Text;
using FitLife.Correlation;
using FitLife.Messaging.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace FitLife.Messaging.IntegrationTests;

/// <summary>An event goes through a real RabbitMQ from the publisher to the consumer's handler, with its correlation ID (#190, #19).</summary>
public sealed class RabbitMqRoundTripTests
{
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
    public async Task Publish_ReservationMade_ReachesTheHandler()
    {
        // Arrange
        var handler = new RecordingHandler();
        using IHost host = await StartHostAsync(handler).ConfigureAwait(false);

        // Act
        await host.Services.GetRequiredService<IEventPublisher>().PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Received received = await handler.First.WaitAsync(Timeout).ConfigureAwait(false);
        Assert.That(received.Event, Is.EqualTo(Sample));
    }

    [Test]
    public async Task Publish_InsideACorrelationId_TheHandlerRunsWithTheSameId()
    {
        // Arrange
        var handler = new RecordingHandler();
        using IHost host = await StartHostAsync(handler).ConfigureAwait(false);
        IEventPublisher publisher = host.Services.GetRequiredService<IEventPublisher>();

        // Act
        using (CorrelationContext.Begin(NullLogger.Instance, "abc-123"))
        {
            await publisher.PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);
        }

        // Assert
        Received received = await handler.First.WaitAsync(Timeout).ConfigureAwait(false);
        Assert.That(received.CorrelationId, Is.EqualTo("abc-123"));
    }

    [Test]
    public async Task Publish_WithoutACorrelationId_TheHandlerRunsWithANewId()
    {
        // Arrange
        var handler = new RecordingHandler();
        using IHost host = await StartHostAsync(handler).ConfigureAwait(false);

        // Act
        await host.Services.GetRequiredService<IEventPublisher>().PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Received received = await handler.First.WaitAsync(Timeout).ConfigureAwait(false);
        Assert.That(received.CorrelationId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Consume_AMessageThatIsNotTheEvent_SkipsItAndHandlesTheNext()
    {
        // Arrange
        var handler = new RecordingHandler();
        using IHost host = await StartHostAsync(handler).ConfigureAwait(false);
        await PublishRawAsync("not json").ConfigureAwait(false);

        // Act
        await host.Services.GetRequiredService<IEventPublisher>().PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Received received = await handler.First.WaitAsync(Timeout).ConfigureAwait(false);
        Assert.That(received.Event, Is.EqualTo(Sample));
    }

    /// <summary>A host with a publisher and a consumer on a queue of its own, so the tests don't share messages.</summary>
    private async Task<IHost> StartHostAsync(RecordingHandler handler)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        var broker = new Uri(_rabbitMq.GetConnectionString());
        string[] credentials = broker.UserInfo.Split(':');
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["RabbitMq:HostName"] = broker.Host,
            ["RabbitMq:Port"] = broker.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RabbitMq:UserName"] = credentials[0],
            ["RabbitMq:Password"] = credentials[1],
        });
        builder.Services.AddRabbitMq(builder.Configuration);
        builder.Services.AddEventPublisher();
        builder.Services.AddSingleton<IEventSubscriber<ReservationMade>>(handler);
        builder.Services.AddEventConsumer<ReservationMade>("test-" + Guid.NewGuid().ToString("N"));
        IHost host = builder.Build();
        await host.StartAsync().ConfigureAwait(false);
        return host;
    }

    /// <summary>A message straight on the exchange, bypassing the publisher, as a broken producer would send it.</summary>
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

    private sealed record Received(ReservationMade Event, string? CorrelationId);

    private sealed class RecordingHandler : IEventSubscriber<ReservationMade>
    {
        private readonly TaskCompletionSource<Received> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Received> First => _first.Task;

        public Task HandleAsync(ReservationMade integrationEvent, CancellationToken cancellationToken)
        {
            _first.TrySetResult(new Received(integrationEvent, CorrelationContext.Current));
            return Task.CompletedTask;
        }
    }
}
