using System.Text;
using FitLife.Contracts.Testing;
using FitLife.Correlation;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Models;
using NotificationService.Subscribers;

namespace NotificationService.Tests;

/// <summary>Notification tells everyone reserved or waitlisted on a cancelled class session (#222).</summary>
public sealed class ClassSessionCancelledSubscriberTests
{
    private static readonly ClassSessionCancelled ForThreeMembers = new(
        "class-session-1",
        ["member-1", "member-2", "member-3"],
        new DateTimeOffset(2026, 12, 1, 18, 0, 0, TimeSpan.FromHours(1)));

    private static readonly string[] StoredIds =
    [
        "class-session-cancelled:class-session-1:member-1",
        "class-session-cancelled:class-session-1:member-2",
        "class-session-cancelled:class-session-1:member-3",
    ];

    private readonly InMemoryNotificationRepository _repository = new();
    private readonly CapturingLogger<ClassSessionCancelledSubscriber> _logger = new();

    [Test]
    public async Task HandleAsync_TheContractExample_StoresANotification()
    {
        // Arrange
        ClassSessionCancelled example = EventJson.Deserialize<ClassSessionCancelled>(Encoding.UTF8.GetBytes(EventSchemas.Example("class-session-cancelled")));

        // Act
        await Subscriber().HandleAsync(example, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(_repository.Stored.Select(notification => notification.MemberId), Is.EquivalentTo(example.MemberIds));
    }

    [Test]
    public async Task HandleAsync_ClassSessionCancelledForThreeMembers_StoresOneNotificationEach()
    {
        // Act
        await Subscriber().HandleAsync(ForThreeMembers, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(_repository.Stored.Select(notification => notification.Id), Is.EquivalentTo(StoredIds));
    }

    [Test]
    public async Task HandleAsync_ClassSessionCancelled_EachNotificationIsForItsMemberWithTheKindAndTheTime()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 11, 30, 9, 15, 0, TimeSpan.Zero);

        // Act
        await new ClassSessionCancelledSubscriber(_repository, new FixedTime(now), _logger).HandleAsync(ForThreeMembers, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(
            _repository.Stored.Select(notification => (notification.Id, notification.MemberId, notification.Kind, notification.OccurredAt)),
            Is.EquivalentTo(ForThreeMembers.MemberIds.Select(memberId => ($"class-session-cancelled:class-session-1:{memberId}", memberId, "ClassSessionCancelled", now))));
    }

    [Test]
    public async Task HandleAsync_ClassSessionCancelled_TextIsDanishAndNamesTheStart()
    {
        // Act
        await Subscriber().HandleAsync(ForThreeMembers with { MemberIds = ["member-1"] }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(_repository.Stored.Single().Text, Is.EqualTo("Dit hold den 01.12.2026 kl. 18:00 er aflyst. Din booking eller ventelisteplads er fjernet."));
    }

    [Test]
    public async Task HandleAsync_ClassSessionCancelledTwice_StoresNothingNew()
    {
        // Arrange
        ClassSessionCancelledSubscriber subscriber = Subscriber();
        await subscriber.HandleAsync(ForThreeMembers, CancellationToken.None).ConfigureAwait(false);
        Notification[] before = [.. _repository.Stored];

        // Act
        await subscriber.HandleAsync(ForThreeMembers, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(_repository.Stored, Is.EquivalentTo(before));
    }

    [Test]
    public async Task HandleAsync_ClassSessionCancelledTwice_LogsOneLinePerMember()
    {
        // Arrange
        ClassSessionCancelledSubscriber subscriber = Subscriber();
        await subscriber.HandleAsync(ForThreeMembers, CancellationToken.None).ConfigureAwait(false);

        // Act
        await subscriber.HandleAsync(ForThreeMembers, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(_logger.NotificationLines.Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task HandleAsync_NoMemberIds_StoresAndLogsNothing()
    {
        // Act
        await Subscriber().HandleAsync(ForThreeMembers with { MemberIds = [] }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((_repository.Stored.Count, _logger.NotificationLines.Count()), Is.EqualTo((0, 0)));
    }

    [Test]
    public async Task HandleAsync_InsideACorrelationId_EachLineHasItsMemberAndTheId()
    {
        // Act
        using (CorrelationContext.Begin(NullLogger.Instance, "corr-1"))
        {
            await Subscriber().HandleAsync(ForThreeMembers with { MemberIds = ["member-1", "member-2"] }, CancellationToken.None).ConfigureAwait(false);
        }

        // Assert
        Assert.That(_logger.NotificationLines, Is.EquivalentTo(new[]
        {
            "Notification to member member-1: Dit hold den 01.12.2026 kl. 18:00 er aflyst. Din booking eller ventelisteplads er fjernet. (correlation ID corr-1)",
            "Notification to member member-2: Dit hold den 01.12.2026 kl. 18:00 er aflyst. Din booking eller ventelisteplads er fjernet. (correlation ID corr-1)",
        }));
    }

    private ClassSessionCancelledSubscriber Subscriber()
    {
        return new ClassSessionCancelledSubscriber(_repository, TimeProvider.System, _logger);
    }
}
