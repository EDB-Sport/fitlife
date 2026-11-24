using FitLife.Correlation;
using FitLife.Messaging.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;

namespace FitLife.Messaging.Tests;

/// <summary>What the publisher puts on the wire (#190).</summary>
public sealed class RabbitMqEventPublisherTests
{
    private static readonly ReservationMade Sample = new("reservation-1", "class-session-1", "member-1", new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task PublishAsync_ReservationMade_GoesToTheEventsExchangeWithItsRoutingKey()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        var publisher = new RabbitMqEventPublisher(rabbitMq.Client);

        // Act
        await publisher.PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        FakeRabbitMq.PublishedMessage message = rabbitMq.Published.Single();
        Assert.That((message.Exchange, message.RoutingKey), Is.EqualTo(("fitlife.events", "reservation-made")));
    }

    [Test]
    public async Task PublishAsync_ReservationMade_SendsTheEventAsJson()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        var publisher = new RabbitMqEventPublisher(rabbitMq.Client);

        // Act
        await publisher.PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(EventJson.Deserialize<ReservationMade>(rabbitMq.Published.Single().Body), Is.EqualTo(Sample));
    }

    [Test]
    public async Task PublishAsync_ReservationMade_IsPersistentJsonWithAMessageIdAndItsType()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        var publisher = new RabbitMqEventPublisher(rabbitMq.Client);

        // Act
        await publisher.PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        BasicProperties properties = rabbitMq.Published.Single().Properties;
        Assert.Multiple(() =>
        {
            Assert.That(properties.DeliveryMode, Is.EqualTo(DeliveryModes.Persistent));
            Assert.That(properties.ContentType, Is.EqualTo("application/json"));
            Assert.That(properties.Type, Is.EqualTo("reservation-made"));
            Assert.That(Guid.TryParse(properties.MessageId, out _), Is.True);
        });
    }

    [Test]
    public async Task PublishAsync_InsideACorrelationId_PutsItInTheHeaders()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        var publisher = new RabbitMqEventPublisher(rabbitMq.Client);

        // Act
        using (CorrelationContext.Begin(NullLogger.Instance, "abc-123"))
        {
            await publisher.PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);
        }

        // Assert
        Assert.That(CorrelationHeaders.Read(rabbitMq.Published.Single().Properties.Headers), Is.EqualTo("abc-123"));
    }

    [Test]
    public async Task PublishAsync_ReservationMade_DeclaresTheExchangeAsADurableTopic()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        var publisher = new RabbitMqEventPublisher(rabbitMq.Client);

        // Act
        await publisher.PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        rabbitMq.Channel.Verify(channel => channel.ExchangeDeclareAsync("fitlife.events", ExchangeType.Topic, true, false, It.IsAny<IDictionary<string, object?>?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task PublishAsync_TwoEvents_OpensOneConnection()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        var publisher = new RabbitMqEventPublisher(rabbitMq.Client);
        await publisher.PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Act
        await publisher.PublishAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(rabbitMq.Attempts, Is.EqualTo(1));
    }
}
