using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookingService.Api;
using BookingService.Domain;
using FitLife.Authentication;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>A Træner or an Admin cancels a class session with <c>POST /class-sessions/{id}/cancellation</c> (#239).</summary>
public sealed class CancelClassSessionApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Uri ClassSessions = new("/class-sessions", UriKind.Relative);

    [Test]
    public async Task PostCancellation_WithTheRoleTraener_ReturnsNoContentAndTheScheduleShowsItCancelled()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);

        // Act
        using HttpResponseMessage response = await client.PostAsync(CancellationOf(classSession.Id), content: null).ConfigureAwait(false);

        // Assert
        List<ClassSessionResponse>? schedule = await client.GetFromJsonAsync<List<ClassSessionResponse>>(ClassSessions).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(schedule?.Single().IsCancelled, Is.True);
        });
    }

    [Test]
    public async Task PostCancellation_WithTheRoleAdmin_ReturnsNoContent()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.PostAsync(CancellationOf(classSession.Id), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task PostCancellation_Twice_ReturnsConflict()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);
        using HttpResponseMessage first = await client.PostAsync(CancellationOf(classSession.Id), content: null).ConfigureAwait(false);

        // Act
        using HttpResponseMessage second = await client.PostAsync(CancellationOf(classSession.Id), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(await StatusAndCodeAsync(second).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "ClassSessionAlreadyCancelled")));
    }

    [Test]
    public async Task PostCancellation_OfAnUnknownClassSession_ReturnsNotFound()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);

        // Act
        using HttpResponseMessage response = await client.PostAsync(CancellationOf(Guid.NewGuid()), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(await StatusAndCodeAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.NotFound, "ClassSessionNotFound")));
    }

    [Test]
    public async Task PostCancellation_WithoutAToken_ReturnsUnauthorized()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        // Act
        using HttpResponseMessage response = await client.PostAsync(CancellationOf(classSession.Id), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PostCancellation_WithOnlyTheRoleMedlem_ReturnsForbidden()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Medlem);

        // Act
        using HttpResponseMessage response = await client.PostAsync(CancellationOf(classSession.Id), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task PostReservation_OnACancelledClassSession_ReturnsConflict()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient traener = ClientWithRole(factory, FitLifeRoles.Traener);
        using HttpResponseMessage cancellation = await traener.PostAsync(CancellationOf(classSession.Id), content: null).ConfigureAwait(false);
        using HttpClient member = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await member.PostAsync(new Uri($"/class-sessions/{classSession.Id}/reservations", UriKind.Relative), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(await StatusAndCodeAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "ClassSessionCancelled")));
    }

    private static Uri CancellationOf(Guid classSessionId)
    {
        return new Uri($"/class-sessions/{classSessionId}/cancellation", UriKind.Relative);
    }

    private static async Task<ClassSession> StoredAsync(BookingApiFactory factory)
    {
        var classSession = new ClassSession(
            Guid.NewGuid(), new Capacity(2), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        await factory.AddAsync(classSession).ConfigureAwait(false);
        return classSession;
    }

    private static HttpClient ClientWithRole(BookingApiFactory factory, string role)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(Guid.NewGuid(), role));
        return client;
    }

    /// <summary>The status code and the problem details' <c>code</c>, which a client switches on.</summary>
    private static async Task<(HttpStatusCode Status, string? Code)> StatusAndCodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        string? code = null;
        if (problem.RootElement.TryGetProperty("code", out JsonElement value))
        {
            code = value.GetString();
        }

        return (response.StatusCode, code);
    }

    private static BookingApiFactory Factory()
    {
        return new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider(Now));
    }
}
