using FitLife.Messaging.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace FitLife.Messaging.Tests;

/// <summary>What /health says about a consumer that has started consuming (#302).</summary>
public sealed class RabbitMqConsumerHealthCheckTests
{
    [Test]
    public async Task CheckHealth_TheChannelClosedAfterItStartedConsuming_IsUnhealthyAndNamesTheQueue()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        using RabbitMqEventConsumer<ReservationMade> consumer = await StartConsumingAsync(rabbitMq).ConfigureAwait(false);
        rabbitMq.Channel.SetupGet(channel => channel.IsOpen).Returns(false);

        // Act
        HealthCheckResult result = await CheckAsync(consumer).ConfigureAwait(false);

        // Assert
        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
        Assert.That(result.Description, Does.Contain(consumer.Queue));
    }

    [Test]
    public async Task CheckHealth_TheChannelOpenAgain_IsHealthy()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        using RabbitMqEventConsumer<ReservationMade> consumer = await StartConsumingAsync(rabbitMq).ConfigureAwait(false);
        rabbitMq.Channel.SetupSequence(channel => channel.IsOpen).Returns(false).Returns(true);
        await CheckAsync(consumer).ConfigureAwait(false);

        // Act
        HealthCheckResult result = await CheckAsync(consumer).ConfigureAwait(false);

        // Assert
        Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
    }

    private static async Task<RabbitMqEventConsumer<ReservationMade>> StartConsumingAsync(FakeRabbitMq rabbitMq)
    {
        ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        var consumer = new RabbitMqEventConsumer<ReservationMade>(rabbitMq.Client, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<RabbitMqEventConsumer<ReservationMade>>.Instance, new FakeTimeProvider(), "notification");
        await consumer.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await consumer.Consuming.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        return consumer;
    }

    private static Task<HealthCheckResult> CheckAsync(RabbitMqEventConsumer<ReservationMade> consumer)
    {
        return new RabbitMqConsumerHealthCheck<ReservationMade>(consumer).CheckHealthAsync(new HealthCheckContext());
    }
}
