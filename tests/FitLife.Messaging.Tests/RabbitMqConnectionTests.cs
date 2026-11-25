using Moq;

namespace FitLife.Messaging.Tests;

/// <summary>The service's one RabbitMQ connection (#190).</summary>
public sealed class RabbitMqConnectionTests
{
    [Test]
    public async Task Dispose_AfterAChannel_DisposesTheConnection()
    {
        // Arrange
        var rabbitMq = new FakeRabbitMq();
        await rabbitMq.Client.CreateChannelAsync(CancellationToken.None).ConfigureAwait(false);

        // Act
        rabbitMq.Dispose();

        // Assert
        rabbitMq.Connection.Verify(connection => connection.Dispose());
    }

    [Test]
    public async Task DisposeAsync_AfterAChannel_DisposesTheConnection()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        await rabbitMq.Client.CreateChannelAsync(CancellationToken.None).ConfigureAwait(false);

        // Act
        await rabbitMq.Client.DisposeAsync().ConfigureAwait(false);

        // Assert
        rabbitMq.Connection.Verify(connection => connection.DisposeAsync());
    }

    [Test]
    public void Defaults_AreTheLocalBrokerOnTheAmqpPort()
    {
        // Act
        var options = new RabbitMqOptions();

        // Assert
        Assert.That((options.HostName, options.Port, options.UserName, options.Password), Is.EqualTo(("localhost", 5672, string.Empty, string.Empty)));
    }
}
