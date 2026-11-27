using Microsoft.Extensions.DependencyInjection;
using NotificationService.Models;
using NotificationService.Repositories;

namespace NotificationService.IntegrationTests;

/// <summary>The delivery log in the real MongoDB: a member's notifications, newest first (#221).</summary>
public sealed class NotificationRepositoryTests
{
    [Test]
    public async Task ListForMember_TwoNotifications_ReturnsNewestFirst()
    {
        // Arrange: the older one stored first, so the order can't come from the insert order
        await using var factory = new NotificationServiceFactory();
        INotificationRepository notifications = factory.Services.GetRequiredService<INotificationRepository>();
        string memberId = "member-" + Guid.NewGuid().ToString("N");
        var older = new Notification("older-" + memberId, memberId, "ReservationMade", "First.", new DateTimeOffset(2026, 12, 1, 8, 0, 0, TimeSpan.Zero));
        var newer = new Notification("newer-" + memberId, memberId, "ReservationMade", "Second.", new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero));
        await notifications.TryAddAsync(older, CancellationToken.None).ConfigureAwait(false);
        await notifications.TryAddAsync(newer, CancellationToken.None).ConfigureAwait(false);

        // Act
        IReadOnlyList<Notification> listed = await notifications.ListForMemberAsync(memberId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(listed, Is.EqualTo(new[] { newer, older }));
    }
}
