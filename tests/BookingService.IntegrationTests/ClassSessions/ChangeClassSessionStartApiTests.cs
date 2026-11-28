using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookingService.Api;
using BookingService.Application;
using BookingService.Domain;
using FitLife.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>A Træner or an Admin moves a class session with <c>PUT /class-sessions/{id}/start</c> (#241).</summary>
public sealed class ChangeClassSessionStartApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NewStart = Now.AddDays(2);
    private static readonly Uri ClassSessions = new("/class-sessions", UriKind.Relative);

    [Test]
    public async Task PutStart_WithTheRoleTraener_ReturnsNoContentAndTheScheduleShowsTheNewStart()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);

        // Act
        using HttpResponseMessage response = await client.PutAsJsonAsync(StartOf(classSession.Id), new ChangeStartRequest(NewStart)).ConfigureAwait(false);

        // Assert
        List<ClassSessionResponse>? schedule = await client.GetFromJsonAsync<List<ClassSessionResponse>>(ClassSessions).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(schedule?.Single().StartTime, Is.EqualTo(NewStart));
        });
    }

    [Test]
    public async Task PutStart_WithTheRoleAdmin_ReturnsNoContent()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.PutAsJsonAsync(StartOf(classSession.Id), new ChangeStartRequest(NewStart)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task PutStart_OnACancelledClassSession_ReturnsConflict()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);
        using HttpResponseMessage cancellation = await client.PostAsync(new Uri($"/class-sessions/{classSession.Id}/cancellation", UriKind.Relative), content: null).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.PutAsJsonAsync(StartOf(classSession.Id), new ChangeStartRequest(NewStart)).ConfigureAwait(false);

        // Assert
        (HttpStatusCode Status, string? Code) problem = await StatusAndCodeAsync(response).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.EqualTo((HttpStatusCode.Conflict, "ClassSessionCancelled")));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });
    }

    [Test]
    public async Task PutStart_ToNow_ReturnsConflictAndSavesNothing()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);

        // Act
        using HttpResponseMessage response = await client.PutAsJsonAsync(StartOf(classSession.Id), new ChangeStartRequest(Now)).ConfigureAwait(false);

        // Assert
        (HttpStatusCode Status, string? Code) problem = await StatusAndCodeAsync(response).ConfigureAwait(false);
        ClassSession? stored = await ReadAsync(factory, classSession.Id).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.EqualTo((HttpStatusCode.Conflict, "StartNotInTheFuture")));
            Assert.That(stored?.StartTime, Is.EqualTo(classSession.StartTime));
        });
    }

    [Test]
    public async Task PutStart_AfterTheClassSessionHasStarted_ReturnsConflict()
    {
        // Arrange: started five minutes ago (#538)
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory, Now.AddMinutes(-5)).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);

        // Act
        using HttpResponseMessage response = await client.PutAsJsonAsync(StartOf(classSession.Id), new ChangeStartRequest(NewStart)).ConfigureAwait(false);

        // Assert
        Assert.That(await StatusAndCodeAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "ClassSessionStarted")));
    }

    [Test]
    public async Task PutStart_OfAnUnknownClassSession_ReturnsNotFound()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);

        // Act
        using HttpResponseMessage response = await client.PutAsJsonAsync(StartOf(Guid.NewGuid()), new ChangeStartRequest(NewStart)).ConfigureAwait(false);

        // Assert
        Assert.That(await StatusAndCodeAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.NotFound, "ClassSessionNotFound")));
    }

    [Test]
    public async Task PutStart_WithoutAToken_ReturnsUnauthorized()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        // Act
        using HttpResponseMessage response = await client.PutAsJsonAsync(StartOf(classSession.Id), new ChangeStartRequest(NewStart)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PutStart_WithOnlyTheRoleMedlem_ReturnsForbidden()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Medlem);

        // Act
        using HttpResponseMessage response = await client.PutAsJsonAsync(StartOf(classSession.Id), new ChangeStartRequest(NewStart)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    private static Uri StartOf(Guid classSessionId)
    {
        return new Uri($"/class-sessions/{classSessionId}/start", UriKind.Relative);
    }

    private static Task<ClassSession> StoredAsync(BookingApiFactory factory)
    {
        return StoredAsync(factory, Now.AddDays(1));
    }

    private static async Task<ClassSession> StoredAsync(BookingApiFactory factory, DateTimeOffset startTime)
    {
        var classSession = new ClassSession(
            Guid.NewGuid(), new Capacity(2), startTime, new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        await factory.AddAsync(classSession).ConfigureAwait(false);
        return classSession;
    }

    private static async Task<ClassSession?> ReadAsync(BookingApiFactory factory, Guid classSessionId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IClassSessionRepository>().GetByIdAsync(classSessionId, CancellationToken.None).ConfigureAwait(false);
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
