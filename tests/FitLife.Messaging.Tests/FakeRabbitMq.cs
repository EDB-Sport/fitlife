using Moq;
using RabbitMQ.Client;

namespace FitLife.Messaging.Tests;

/// <summary>A mocked RabbitMQ connection with one channel, that records what is published and which consumer listens.</summary>
internal sealed class FakeRabbitMq : IDisposable
{
    public FakeRabbitMq()
    {
        Client = new RabbitMqConnection(_ =>
        {
            Attempts++;
            if (Attempts <= Failures)
            {
                throw new InvalidOperationException("RabbitMQ is not reachable.");
            }

            return Task.FromResult(Connection.Object);
        });
        Channel.SetupGet(channel => channel.IsOpen).Returns(true);
        Channel
            .Setup(channel => channel.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(invocation => Published.Add(new PublishedMessage(
                invocation.Arguments[0]?.ToString() ?? string.Empty,
                invocation.Arguments[1]?.ToString() ?? string.Empty,
                invocation.Arguments[3] as BasicProperties ?? new BasicProperties(),
                invocation.Arguments[4] is ReadOnlyMemory<byte> body ? body.ToArray() : []))))
            .Returns(ValueTask.CompletedTask);
        Channel
            .Setup(channel => channel.BasicConsumeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object?>?>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(invocation => Consumer = invocation.Arguments[6] as IAsyncBasicConsumer))
            .ReturnsAsync("consumer-tag");
        Connection
            .Setup(connection => connection.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Channel.Object);
    }

    public Mock<IConnection> Connection { get; } = new();

    public Mock<IChannel> Channel { get; } = new();

    public List<PublishedMessage> Published { get; } = [];

    public IAsyncBasicConsumer? Consumer { get; private set; }

    /// <summary>Gets or sets how many tries to open the connection fail before one succeeds, as when RabbitMQ is not up yet.</summary>
    public int Failures { get; set; }

    /// <summary>Gets how many times a connection was opened or tried.</summary>
    public int Attempts { get; private set; }

    /// <summary>Gets the <see cref="RabbitMqConnection"/> that opens <see cref="Connection"/>, after <see cref="Failures"/> failed tries.</summary>
    public RabbitMqConnection Client { get; }

    public void Dispose()
    {
        Client.Dispose();
    }

    /// <summary>Delivers a message to the consumer, as RabbitMQ does.</summary>
    public Task DeliverAsync(ulong deliveryTag, byte[] body, IDictionary<string, object?>? headers = null)
    {
        IAsyncBasicConsumer consumer = Consumer ?? throw new InvalidOperationException("Nothing consumes yet.");
        var properties = new BasicProperties { Headers = headers };
        return consumer.HandleBasicDeliverAsync("consumer-tag", deliveryTag, redelivered: false, "fitlife.events", "reservation-made", properties, body);
    }

    internal sealed record PublishedMessage(string Exchange, string RoutingKey, BasicProperties Properties, byte[] Body);
}
