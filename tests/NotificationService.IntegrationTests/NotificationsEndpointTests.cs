using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FitLife.Authentication;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Models;
using NotificationService.Repositories;

namespace NotificationService.IntegrationTests;

/// <summary>The delivery log for staff: <c>GET /notifications?memberId={id}</c>, for Træner and Admin only (#221).</summary>
public sealed class NotificationsEndpointTests
{
    private readonly NotificationServiceFactory _factory = new();

    [OneTimeTearDown]
    public async Task Stop()
    {
        await _factory.DisposeAsync().ConfigureAwait(false);
    }

    [Test]
    public async Task GetNotifications_WithoutAToken_Returns401()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/notifications?memberId=member-1", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetNotifications_WithRoleMedlem_Returns403()
    {
        // Arrange
        using HttpClient client = ClientWith(FitLifeRoles.Medlem);

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/notifications?memberId=member-1", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [TestCase("Træner")]
    [TestCase("Admin")]
    public async Task GetNotifications_WithAStaffRole_ReturnsTheMembersNotifications(string role)
    {
        // Arrange: two notifications to the member, the older one stored first, and one to someone else
        string memberId = "member-" + Guid.NewGuid().ToString("N");
        var older = new Notification("older-" + memberId, memberId, "ReservationMade", "Your seat is reserved.", new DateTimeOffset(2026, 12, 1, 8, 0, 0, TimeSpan.Zero));
        var newer = new Notification("newer-" + memberId, memberId, "ReservationMade", "Your other seat is reserved.", new DateTimeOffset(2026, 12, 2, 8, 0, 0, TimeSpan.Zero));
        var someoneElses = new Notification("other-" + memberId, "other-" + memberId, "ReservationMade", "Not yours.", new DateTimeOffset(2026, 12, 3, 8, 0, 0, TimeSpan.Zero));
        using HttpClient client = ClientWith(role);
        INotificationRepository notifications = _factory.Services.GetRequiredService<INotificationRepository>();
        foreach (Notification notification in new[] { older, newer, someoneElses })
        {
            await notifications.TryAddAsync(notification, CancellationToken.None).ConfigureAwait(false);
        }

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri($"/notifications?memberId={memberId}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        JsonElement[] items = [.. body.RootElement.EnumerateArray()];
        Assert.That(items.Select(item => item.GetProperty("id").GetString()), Is.EqualTo(new[] { newer.Id, older.Id }));
        JsonElement first = items[0];
        Assert.Multiple(() =>
        {
            Assert.That(first.GetProperty("memberId").GetString(), Is.EqualTo(memberId));
            Assert.That(first.GetProperty("kind").GetString(), Is.EqualTo("ReservationMade"));
            Assert.That(first.GetProperty("text").GetString(), Is.EqualTo(newer.Text));
            Assert.That(first.GetProperty("occurredAt").GetDateTimeOffset(), Is.EqualTo(newer.OccurredAt));
        });
    }

    [Test]
    public async Task GetNotifications_AMemberWithNone_ReturnsAnEmptyList()
    {
        // Arrange
        using HttpClient client = ClientWith(FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/notifications?memberId=member-without-notifications-" + Guid.NewGuid().ToString("N"), UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync().ConfigureAwait(false), Is.EqualTo("[]"));
    }

    [TestCase("/notifications")]
    [TestCase("/notifications?memberId=")]
    public async Task GetNotifications_WithoutAMemberId_Returns400(string path)
    {
        // Arrange
        using HttpClient client = ClientWith(FitLifeRoles.Traener);

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }

    private HttpClient ClientWith(string role)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(role));
        return client;
    }
}
