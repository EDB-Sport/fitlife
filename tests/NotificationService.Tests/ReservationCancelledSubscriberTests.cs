using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Correlation;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Models;
using NotificationService.Subscribers;

namespace NotificationService.Tests;

/// <summary>Notification confirms a cancelled reservation to the member (#222).</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // a fresh repository and logger for every test
public sealed class ReservationCancelledSubscriberTests
{
    private static readonly DateTimeOffset Start = new(2026, 12, 1, 18, 0, 0, TimeSpan.FromHours(1));

    private static readonly ReservationCancelled Early = new("reservation-1", "class-session-1", "member-1", Start, IsLateCancellation: false);

    private static readonly string[] CorrelatedLine = ["Notification to member member-1: Din booking på holdet den 01.12.2026 kl. 18:00 er afmeldt. (correlation ID corr-1)"];

    private readonly InMemoryNotificationRepository _repository = new();
    private readonly CapturingLogger<ReservationCancelledSubscriber> _logger = new();

    [Test]
    public async Task HandleAsync_TheContractExample_StoresANotification()
    {
        // Arrange
        ReservationCancelled example = EventJson.Deserialize<ReservationCancelled>(Encoding.UTF8.GetBytes(EventSchemas.Example("reservation-cancelled")));

        // Act
        await Subscriber().HandleAsync(example, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(_repository.Stored.Select(notification => notification.MemberId), Is.EqualTo(new[] { example.MemberId }));
    }

    [Test]
    public async Task HandleAsync_ReservationCancelled_TextIsDanishAndNamesTheStart()
    {
        // Act
        await Subscriber().HandleAsync(Early, CancellationToken.None).ConfigureAwait(false);

        // Assert: the start in the offset the event carries
        Assert.That(_repository.Stored.Single().Text, Is.EqualTo("Din booking på holdet den 01.12.2026 kl. 18:00 er afmeldt."));
    }

    [Test]
    public async Task HandleAsync_LateCancellation_StoresTheSameTextAsAnEarlyOne()
    {
        // Arrange: Sen afmelding is statistics only
        var earlyRepository = new InMemoryNotificationRepository();
        await new ReservationCancelledSubscriber(earlyRepository, TimeProvider.System, _logger).HandleAsync(Early, CancellationToken.None).ConfigureAwait(false);

        // Act
        await Subscriber().HandleAsync(Early with { IsLateCancellation = true }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(_repository.Stored.Single().Text, Is.EqualTo(earlyRepository.Stored.Single().Text));
    }

    [Test]
    public async Task HandleAsync_ReservationCancelled_StoresTheMemberTheKindAndTheTime()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 11, 30, 9, 15, 0, TimeSpan.Zero);

        // Act
        await new ReservationCancelledSubscriber(_repository, new FixedTime(now), _logger).HandleAsync(Early, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Notification stored = _repository.Stored.Single();
        Assert.That((stored.Id, stored.MemberId, stored.Kind, stored.OccurredAt), Is.EqualTo(("reservation-cancelled:reservation-1", "member-1", "ReservationCancelled", now)));
    }

    [Test]
    public async Task HandleAsync_TheSameReservationCancelledTwice_StoresAndLogsOneNotification()
    {
        // Arrange
        ReservationCancelledSubscriber subscriber = Subscriber();
        await subscriber.HandleAsync(Early, CancellationToken.None).ConfigureAwait(false);

        // Act
        await subscriber.HandleAsync(Early, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((_repository.Stored.Count, _logger.NotificationLines.Count()), Is.EqualTo((1, 1)));
    }

    [Test]
    public async Task HandleAsync_InsideACorrelationId_LogsOneLineWithTheMemberTheTextAndTheId()
    {
        // Act
        using (CorrelationContext.Begin(NullLogger.Instance, "corr-1"))
        {
            await Subscriber().HandleAsync(Early, CancellationToken.None).ConfigureAwait(false);
        }

        // Assert
        Assert.That(_logger.NotificationLines, Is.EqualTo(CorrelatedLine));
    }

    private ReservationCancelledSubscriber Subscriber()
    {
        return new ReservationCancelledSubscriber(_repository, TimeProvider.System, _logger);
    }
}
