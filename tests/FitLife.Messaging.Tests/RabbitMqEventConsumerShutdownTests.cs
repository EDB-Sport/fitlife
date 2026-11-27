using FitLife.Messaging.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using RabbitMQ.Client;

namespace FitLife.Messaging.Tests;

/// <summary>Stopping and disposing a consumer that is still setting up its channel (#573), on a mocked channel.</summary>
public sealed class RabbitMqEventConsumerShutdownTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Test]
    public async Task DisposeAsync_WhileStillConnecting_DoesNotThrow()
    {
        // Arrange: the channel fails if it is disposed while BasicConsumeAsync still runs, as seen in #573
        using var rabbitMq = new FakeRabbitMq();
        SlowBasicConsume basicConsume = SlowDown(rabbitMq);
        rabbitMq.Channel.Setup(channel => channel.DisposeAsync()).Returns(basicConsume.ThrowWhileRunning);
        RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq);
        await consumer.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await basicConsume.Started.WaitAsync(Timeout).ConfigureAwait(false);

        // Act
        Task disposing = consumer.DisposeAsync().AsTask();
        basicConsume.Answer.SetResult("consumer-tag");

        // Assert
        await Assert.ThatAsync(() => disposing.WaitAsync(Timeout), Throws.Nothing).ConfigureAwait(false);
    }

    [Test]
    public async Task DisposeAsync_WhileStillConnecting_CancelsTheSetUp()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        SlowBasicConsume basicConsume = SlowDown(rabbitMq);
        RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq);
        await consumer.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await basicConsume.Started.WaitAsync(Timeout).ConfigureAwait(false);

        // Act: read the token before BasicConsumeAsync answers, so the set-up is still running
        Task disposing = consumer.DisposeAsync().AsTask();
        bool cancelled = basicConsume.Token.IsCancellationRequested;
        basicConsume.Answer.SetResult("consumer-tag");
        await disposing.WaitAsync(Timeout).ConfigureAwait(false);

        // Assert
        Assert.That(cancelled, Is.True);
    }

    [Test]
    public async Task StopAsync_WhileStillConnecting_ClosesTheChannelAfterTheSetUp()
    {
        // Arrange
        using var rabbitMq = new FakeRabbitMq();
        SlowBasicConsume basicConsume = SlowDown(rabbitMq);
        rabbitMq.Channel
            .Setup(channel => channel.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(() => basicConsume.ThrowWhileRunning().AsTask());
        using RabbitMqEventConsumer<ReservationMade> consumer = Create(rabbitMq);
        await consumer.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await basicConsume.Started.WaitAsync(Timeout).ConfigureAwait(false);

        // Act
        Task stopping = consumer.StopAsync(CancellationToken.None);
        basicConsume.Answer.SetResult("consumer-tag");

        // Assert
        await Assert.ThatAsync(() => stopping.WaitAsync(Timeout), Throws.Nothing).ConfigureAwait(false);
        rabbitMq.Channel.Verify(channel => channel.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));
    }

    /// <summary>Makes BasicConsumeAsync answer only when the test says so, whatever its token says.</summary>
    private static SlowBasicConsume SlowDown(FakeRabbitMq rabbitMq)
    {
        var slow = new SlowBasicConsume();
        rabbitMq.Channel
            .Setup(channel => channel.BasicConsumeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object?>?>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(invocation => slow.Start(invocation.Arguments.OfType<CancellationToken>().Single())))
            .Returns(slow.Answer.Task);
        return slow;
    }

    private static RabbitMqEventConsumer<ReservationMade> Create(FakeRabbitMq rabbitMq)
    {
        ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        return new RabbitMqEventConsumer<ReservationMade>(rabbitMq.Client, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<RabbitMqEventConsumer<ReservationMade>>.Instance, new FakeTimeProvider(), "notification");
    }

    /// <summary>A BasicConsumeAsync that runs from <see cref="Start"/> until the test sets <see cref="Answer"/>.</summary>
    private sealed class SlowBasicConsume
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public TaskCompletionSource<string> Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken Token { get; private set; }

        public void Start(CancellationToken token)
        {
            Token = token;
            _started.TrySetResult();
        }

        public ValueTask ThrowWhileRunning()
        {
            if (!Answer.Task.IsCompleted)
            {
                throw new InvalidOperationException("The channel was torn down while BasicConsumeAsync was running.");
            }

            return ValueTask.CompletedTask;
        }
    }
}
