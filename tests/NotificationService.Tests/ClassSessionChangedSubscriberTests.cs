using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Correlation;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Models;
using NotificationService.Subscribers;

namespace NotificationService.Tests;

/// <summary>Notification tells everyone reserved on a class session about its new start (#222).</summary>
public sealed class ClassSessionChangedSubscriberTests
{
    private static readonly ClassSessionChanged Moved = new(
        "class-session-1",
        ["member-1", "member-2"],
        new DateTimeOffset(2026, 12, 1, 18, 0, 0, TimeSpan.FromHours(1)),
        new DateTimeOffset(2026, 12, 2, 19, 30, 0, TimeSpan.FromHours(1)));

    private readonly InMemoryNotificationRepository _repository = new();
    private readonly CapturingLogger<ClassSessionChangedSubscriber> _logger = new();

    [Test]
    public async Task HandleAsync_TheContractExample_StoresANotification()
    {
        // Arrange
        ClassSessionChanged example = EventJson.Deserialize<ClassSessionChanged>(Encoding.UTF8.GetBytes(EventSchemas.Example("class-session-changed")));

        // Act
        await Subscriber().HandleAsync(example, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(_repository.Stored.Select(notification => notification.MemberId), Is.EquivalentTo(example.MemberIds));
    }

    [Test]
    public async Task HandleAsync_ClassSessionChanged_TextNamesTheNewStart()
    {
        // Act
        await Subscriber().HandleAsync(Moved with { MemberIds = ["member-1"] }, CancellationToken.None).ConfigureAwait(false);

        // Assert: the new start, not the previous one, in the offset the event carries
        Assert.That(_repository.Stored.Single().Text, Is.EqualTo("Dit hold er flyttet til den 02.12.2026 kl. 19:30. Din booking gælder stadig, og du kan frit afmelde den."));
    }

    [Test]
    public async Task HandleAsync_ClassSessionChanged_StoresOneNotificationPerMemberWithTheKindAndTheTime()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 11, 30, 9, 15, 0, TimeSpan.Zero);

        // Act
        await new ClassSessionChangedSubscriber(_repository, new FixedTime(now), _logger).HandleAsync(Moved, CancellationToken.None).ConfigureAwait(false);

        // Assert: the key is the class session and the new start (as UTC), per member (contracts/events/README.md)
        Assert.That(
            _repository.Stored.Select(notification => (notification.Id, notification.MemberId, notification.Kind, notification.OccurredAt)),
            Is.EquivalentTo(new[]
            {
                ("class-session-changed:class-session-1:2026-12-02T18:30:00Z:member-1", "member-1", "ClassSessionChanged", now),
                ("class-session-changed:class-session-1:2026-12-02T18:30:00Z:member-2", "member-2", "ClassSessionChanged", now),
            }));
    }

    [Test]
    public async Task HandleAsync_TheSameChangeTwice_StoresAndLogsNothingNew()
    {
        // Arrange
        ClassSessionChangedSubscriber subscriber = Subscriber();
        await subscriber.HandleAsync(Moved, CancellationToken.None).ConfigureAwait(false);

        // Act
        await subscriber.HandleAsync(Moved, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((_repository.Stored.Count, _logger.NotificationLines.Count()), Is.EqualTo((2, 2)));
    }

    [Test]
    public async Task HandleAsync_MovedAgain_StoresANewNotification()
    {
        // Arrange
        ClassSessionChangedSubscriber subscriber = Subscriber();
        ClassSessionChanged once = Moved with { MemberIds = ["member-1"] };
        await subscriber.HandleAsync(once, CancellationToken.None).ConfigureAwait(false);

        // Act: a second change of the same class session is a new event
        await subscriber.HandleAsync(once with { PreviousStartTime = once.StartTime, StartTime = once.StartTime.AddDays(1) }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(_repository.Stored, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task HandleAsync_NoMemberIds_StoresAndLogsNothing()
    {
        // Act
        await Subscriber().HandleAsync(Moved with { MemberIds = [] }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((_repository.Stored.Count, _logger.NotificationLines.Count()), Is.EqualTo((0, 0)));
    }

    [Test]
    public async Task HandleAsync_InsideACorrelationId_TheLineHasTheMemberTheTextAndTheId()
    {
        // Act
        using (CorrelationContext.Begin(NullLogger.Instance, "corr-1"))
        {
            await Subscriber().HandleAsync(Moved with { MemberIds = ["member-1"] }, CancellationToken.None).ConfigureAwait(false);
        }

        // Assert
        Assert.That(_logger.NotificationLines, Is.EqualTo(new[] { "Notification to member member-1: Dit hold er flyttet til den 02.12.2026 kl. 19:30. Din booking gælder stadig, og du kan frit afmelde den. (correlation ID corr-1)" }));
    }

    private ClassSessionChangedSubscriber Subscriber()
    {
        return new ClassSessionChangedSubscriber(_repository, TimeProvider.System, _logger);
    }
}
