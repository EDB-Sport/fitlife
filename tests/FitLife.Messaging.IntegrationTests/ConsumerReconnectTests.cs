using System.Globalization;
using System.Net;
using System.Net.Sockets;
using FitLife.Messaging.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;

namespace FitLife.Messaging.IntegrationTests;

/// <summary>
/// A consumer that has started consuming loses RabbitMQ and gets it back (#302). RabbitMQ.Client's automatic recovery,
/// on by default, reconnects, declares the queue again and consumes it again.
/// </summary>
public sealed class ConsumerReconnectTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    private static readonly ReservationMade Sample = new(
        "6741f0c2a9e1b3d4c5f60718",
        "6741f0c2a9e1b3d4c5f60719",
        "6741f0c2a9e1b3d4c5f6071a",
        new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero));

    private RabbitMqContainer _rabbitMq = null!;

    [SetUp]
    public async Task StartRabbitMq()
    {
        // A fixed host port: Docker gives a random one a new number when the container starts again
        _rabbitMq = new RabbitMqBuilder("rabbitmq:4.4.1-management").WithPortBinding(FreePort(), RabbitMqBuilder.RabbitMqPort).Build();
        await _rabbitMq.StartAsync().ConfigureAwait(false);
    }

    [TearDown]
    public async Task StopRabbitMq()
    {
        await _rabbitMq.DisposeAsync().ConfigureAwait(false);
    }

    [Test]
    public async Task CheckHealth_RabbitMqStoppedWhileConsuming_IsUnhealthyAndNamesTheQueue()
    {
        // Arrange
        using IHost host = await StartHostAsync(new RecordingHandler()).ConfigureAwait(false);

        // Act
        await _rabbitMq.StopAsync().ConfigureAwait(false);

        // Assert
        HealthReport report = await WaitForHealthAsync(host, HealthStatus.Unhealthy).ConfigureAwait(false);
        Assert.That(report.Entries.Values.Select(entry => entry.Description), Has.Some.Contains(ConsumerOf(host).Queue));
    }

    [Test]
    public async Task Publish_AfterRabbitMqCameBack_ReachesTheHandlerAndTheConsumerIsHealthy()
    {
        // Arrange
        var handler = new RecordingHandler();
        using IHost host = await StartHostAsync(handler).ConfigureAwait(false);
        await _rabbitMq.StopAsync().ConfigureAwait(false);
        await WaitForHealthAsync(host, HealthStatus.Unhealthy).ConfigureAwait(false);
        await _rabbitMq.StartAsync().ConfigureAwait(false);
        await WaitForHealthAsync(host, HealthStatus.Healthy).ConfigureAwait(false);

        // Act
        await host.Services.GetRequiredService<IEventPublisher>().PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(await handler.First.WaitAsync(Timeout).ConfigureAwait(false), Is.EqualTo(Sample));
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static RabbitMqEventConsumer<ReservationMade> ConsumerOf(IHost host)
    {
        return host.Services.GetServices<IHostedService>().OfType<RabbitMqEventConsumer<ReservationMade>>().Single();
    }

    /// <summary>Asks the health checks until they answer <paramref name="status"/>, as a probe does.</summary>
    private static async Task<HealthReport> WaitForHealthAsync(IHost host, HealthStatus status)
    {
        HealthCheckService health = host.Services.GetRequiredService<HealthCheckService>();
        using var deadline = new CancellationTokenSource(Timeout);
        while (true)
        {
            HealthReport report = await health.CheckHealthAsync(deadline.Token).ConfigureAwait(false);
            if (report.Status == status)
            {
                return report;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), deadline.Token).ConfigureAwait(false);
        }
    }

    private async Task<IHost> StartHostAsync(RecordingHandler handler)
    {
        var broker = new Uri(_rabbitMq.GetConnectionString());
        string[] credentials = broker.UserInfo.Split(':');
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["RabbitMq:HostName"] = broker.Host,
            ["RabbitMq:Port"] = broker.Port.ToString(CultureInfo.InvariantCulture),
            ["RabbitMq:UserName"] = credentials[0],
            ["RabbitMq:Password"] = credentials[1],
        });
        builder.Services.AddRabbitMq(builder.Configuration);
        builder.Services.AddEventPublisher();
        builder.Services.AddSingleton<IEventSubscriber<ReservationMade>>(handler);
        builder.Services.AddEventConsumer<ReservationMade>("test-reconnect");
        IHost host = builder.Build();
        await host.StartAsync().ConfigureAwait(false);
        await ConsumerOf(host).Consuming.WaitAsync(Timeout).ConfigureAwait(false);
        return host;
    }

    private sealed class RecordingHandler : IEventSubscriber<ReservationMade>
    {
        private readonly TaskCompletionSource<ReservationMade> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ReservationMade> First => _first.Task;

        public Task HandleAsync(ReservationMade integrationEvent, CancellationToken cancellationToken)
        {
            _first.TrySetResult(integrationEvent);
            return Task.CompletedTask;
        }
    }
}
