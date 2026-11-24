using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Correlation;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Models;
using NotificationService.Repositories;
using NotificationService.Subscribers;

namespace NotificationService.Tests;

/// <summary>Notification turns ReservationMade into one logged notification to the member (#191).</summary>
public sealed class ReservationMadeSubscriberTests
{
    private static readonly ReservationMade Sample = new(
        "reservation-1",
        "class-session-1",
        "member-1",
        new DateTimeOffset(2026, 12, 1, 18, 0, 0, TimeSpan.FromHours(1)));

    [Test]
    public async Task HandleAsync_TheSchemasExample_LogsANotificationToTheMember()
    {
        // Arrange
        ReservationMade example = EventJson.Deserialize<ReservationMade>(Encoding.UTF8.GetBytes(EventSchemas.Example("reservation-made")));
        var logger = new CapturingLogger();
        var subscriber = new ReservationMadeSubscriber(new InMemoryNotificationRepository(), logger);

        // Act
        await subscriber.HandleAsync(example, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(logger.Lines, Has.One.Contains(example.MemberId));
    }

    [Test]
    public async Task HandleAsync_TheSameReservationTwice_LogsOneNotification()
    {
        // Arrange
        var logger = new CapturingLogger();
        var subscriber = new ReservationMadeSubscriber(new InMemoryNotificationRepository(), logger);
        await subscriber.HandleAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Act
        await subscriber.HandleAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(logger.Lines.Count(line => line.Contains("Notification to member", StringComparison.Ordinal)), Is.EqualTo(1));
    }

    [Test]
    public async Task HandleAsync_InsideACorrelationId_TheLineHasTheId()
    {
        // Arrange
        var logger = new CapturingLogger();
        var subscriber = new ReservationMadeSubscriber(new InMemoryNotificationRepository(), logger);

        // Act
        using (CorrelationContext.Begin(NullLogger.Instance, "corr-1"))
        {
            await subscriber.HandleAsync(Sample, CancellationToken.None).ConfigureAwait(false);
        }

        // Assert
        Assert.That(logger.Lines, Has.One.Contains("member-1").And.Contains("corr-1"));
    }

    [Test]
    public async Task HandleAsync_ReservationMade_StoresTheNotificationUnderTheReservationId()
    {
        // Arrange
        var repository = new InMemoryNotificationRepository();
        var subscriber = new ReservationMadeSubscriber(repository, new CapturingLogger());

        // Act
        await subscriber.HandleAsync(Sample, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(repository.Stored.Select(notification => notification.Id), Is.EqualTo(new[] { "reservation-made:reservation-1" }));
    }

    /// <summary>The repository's contract: a second notification with the same id is not added.</summary>
    private sealed class InMemoryNotificationRepository : INotificationRepository
    {
        private readonly Dictionary<string, Notification> _stored = new(StringComparer.Ordinal);

        public IEnumerable<Notification> Stored => _stored.Values;

        public Task<bool> TryAddAsync(Notification notification, CancellationToken cancellationToken)
        {
            return Task.FromResult(_stored.TryAdd(notification.Id, notification));
        }
    }

    private sealed class CapturingLogger : ILogger<ReservationMadeSubscriber>
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines => _lines;

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
            _lines.Add(formatter(state, exception));
        }
    }
}
