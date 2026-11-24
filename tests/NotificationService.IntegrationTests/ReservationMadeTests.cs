using System.Collections.Concurrent;
using FitLife.Correlation;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NotificationService.IntegrationTests;

/// <summary>
/// ReservationMade on the real RabbitMQ becomes one logged notification, stored in the real MongoDB (#191).
/// Each test uses its own reservation ids, so the tests don't depend on each other's messages.
/// </summary>
public sealed class ReservationMadeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Test]
    public async Task ReservationMade_InsideACorrelationId_LogsTheMemberIdAndTheCorrelationId()
    {
        // Arrange
        using var logs = new WaitingLoggerProvider();
        using WebApplicationFactory<Program> factory = Start(logs);
        ReservationMade reservationMade = NewReservationMade();

        // Act
        using (CorrelationContext.Begin(NullLogger.Instance, "notification-it-correlation"))
        {
            await PublisherOf(factory).PublishAsync(reservationMade, CancellationToken.None).ConfigureAwait(false);
        }

        // Assert
        string line = await logs.WaitForAsync(reservationMade.MemberId, Timeout).ConfigureAwait(false);
        Assert.That(line, Does.Contain("notification-it-correlation"));
    }

    [Test]
    public async Task ReservationMade_PublishedTwice_LogsOneNotification()
    {
        // Arrange
        using var logs = new WaitingLoggerProvider();
        using WebApplicationFactory<Program> factory = Start(logs);
        IEventPublisher publisher = PublisherOf(factory);
        ReservationMade twice = NewReservationMade();
        ReservationMade after = NewReservationMade();
        await publisher.PublishAsync(twice, CancellationToken.None).ConfigureAwait(false);

        // Act
        await publisher.PublishAsync(twice, CancellationToken.None).ConfigureAwait(false);

        // Assert: one queue, one consumer, so once the next event is logged both copies have been handled
        await publisher.PublishAsync(after, CancellationToken.None).ConfigureAwait(false);
        await logs.WaitForAsync(after.MemberId, Timeout).ConfigureAwait(false);
        Assert.That(logs.Lines.Count(line => line.Contains(twice.MemberId, StringComparison.Ordinal) && line.Contains("Notification to member", StringComparison.Ordinal)), Is.EqualTo(1));
    }

    private static WebApplicationFactory<Program> Start(WaitingLoggerProvider logs)
    {
        var factory = new NotificationServiceFactory(logs);
        // Creating the client starts the host, and the host has bound its queue once it has started
        factory.CreateClient().Dispose();
        return factory;
    }

    private static IEventPublisher PublisherOf(WebApplicationFactory<Program> factory)
    {
        return new RabbitMqEventPublisher(factory.Services.GetRequiredService<RabbitMqConnection>());
    }

    private static ReservationMade NewReservationMade()
    {
        string id = Guid.NewGuid().ToString("N");
        return new ReservationMade("reservation-" + id, "class-session-" + id, "member-" + id, new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero));
    }

    /// <summary>Captures every log line, and lets a test wait for the first line that contains some text.</summary>
    private sealed class WaitingLoggerProvider : ILoggerProvider, ILogger
    {
        private readonly ConcurrentQueue<string> _lines = new();
        private readonly SemaphoreSlim _logged = new(0);

        public IEnumerable<string> Lines => _lines;

        public async Task<string> WaitForAsync(string text, TimeSpan timeout)
        {
            using var deadline = new CancellationTokenSource(timeout);
            while (true)
            {
                string? line = _lines.FirstOrDefault(candidate => candidate.Contains(text, StringComparison.Ordinal));
                if (line is not null)
                {
                    return line;
                }

                await _logged.WaitAsync(deadline.Token).ConfigureAwait(false);
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _lines.Enqueue(formatter(state, exception));
            _logged.Release();
        }

        public void Dispose()
        {
            _logged.Dispose();
        }
    }
}
