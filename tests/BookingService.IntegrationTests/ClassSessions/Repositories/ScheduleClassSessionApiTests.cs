using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookingService.Api;
using FitLife.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BookingService.IntegrationTests.ClassSessions.Repositories;

/// <summary>A Træner or an Admin schedules a class session with <c>POST /class-sessions</c> (#238).</summary>
public sealed class ScheduleClassSessionApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Uri ClassSessions = new("/class-sessions", UriKind.Relative);
    private static readonly Guid CenterId = Guid.NewGuid();
    private static readonly Guid ClassTypeId = Guid.NewGuid();

    [Test]
    public async Task PostClassSession_WithTheRoleTraener_ReturnsCreatedAndTheScheduleListsIt()
    {
        // Arrange
        using BookingApiFactory factory = await FactoryAsync().ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ClassSessions, Request()).ConfigureAwait(false);

        // Assert
        ClassSessionResponse? created = await response.Content.ReadFromJsonAsync<ClassSessionResponse>().ConfigureAwait(false);
        List<ClassSessionResponse>? schedule = await client.GetFromJsonAsync<List<ClassSessionResponse>>(ClassSessions).ConfigureAwait(false);
        var expected = new ClassSessionResponse(
            created?.Id ?? Guid.Empty, CenterId, "FitLife Center 1", ClassTypeId, "Yoga", "Mette", Now.AddDays(1), 60, 20, FreeSeats: 20, IsCancelled: false);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(created, Is.EqualTo(expected));
            Assert.That(schedule, Is.EqualTo(new[] { expected }));
        });
    }

    [Test]
    public async Task PostClassSession_WithTheRoleAdmin_ReturnsCreated()
    {
        // Arrange
        using BookingApiFactory factory = await FactoryAsync().ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ClassSessions, Request()).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task PostClassSession_WithoutAToken_ReturnsUnauthorized()
    {
        // Arrange
        using BookingApiFactory factory = await FactoryAsync().ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ClassSessions, Request()).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PostClassSession_WithOnlyTheRoleMedlem_ReturnsForbidden()
    {
        // Arrange
        using BookingApiFactory factory = await FactoryAsync().ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Medlem);

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ClassSessions, Request()).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task PostClassSession_WithCapacityZero_ReturnsBadRequest()
    {
        // Arrange
        using BookingApiFactory factory = await FactoryAsync().ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ClassSessions, Request() with { Capacity = 0 }).ConfigureAwait(false);

        // Assert
        List<ClassSessionResponse>? schedule = await client.GetFromJsonAsync<List<ClassSessionResponse>>(ClassSessions).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(schedule, Is.Empty);
        });
    }

    [TestCase(0, 60, "The start time must be after now.")]
    [TestCase(1, 0, "The duration must be at least 1 minute.")]
    public async Task PostClassSession_WithAnInvalidValue_ReturnsBadRequestThatSaysWhich(int daysFromNow, int durationMinutes, string title)
    {
        // Arrange
        using BookingApiFactory factory = await FactoryAsync().ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);
        ScheduleClassSessionRequest request = Request() with { StartsAt = Now.AddDays(daysFromNow), DurationMinutes = durationMinutes };

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ClassSessions, request).ConfigureAwait(false);

        // Assert
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>().ConfigureAwait(false);
        Assert.That((response.StatusCode, problem?.Title), Is.EqualTo((HttpStatusCode.BadRequest, title)));
    }

    [Test]
    public async Task PostClassSession_WithABlankInstructorName_ReturnsBadRequest()
    {
        // Arrange
        using BookingApiFactory factory = await FactoryAsync().ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ClassSessions, Request() with { InstructorName = " " }).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase(true, "No such center.")]
    [TestCase(false, "No such class type.")]
    public async Task PostClassSession_WithAnUnknownId_ReturnsNotFound(bool unknownCenter, string title)
    {
        // Arrange
        using BookingApiFactory factory = await FactoryAsync().ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);
        ScheduleClassSessionRequest request = Request() with { ClassTypeId = Guid.NewGuid() };
        if (unknownCenter)
        {
            request = Request() with { CenterId = Guid.NewGuid() };
        }

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ClassSessions, request).ConfigureAwait(false);

        // Assert
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>().ConfigureAwait(false);
        Assert.That((response.StatusCode, problem?.Title), Is.EqualTo((HttpStatusCode.NotFound, title)));
    }

    private static ScheduleClassSessionRequest Request()
    {
        return new ScheduleClassSessionRequest(CenterId, ClassTypeId, "Mette", Now.AddDays(1), 60, 20);
    }

    private static HttpClient ClientWithRole(BookingApiFactory factory, string role)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(Guid.NewGuid(), role));
        return client;
    }

    /// <summary>A service on its own database, with one centre and one class type of its own: no demo data.</summary>
    private static async Task<BookingApiFactory> FactoryAsync()
    {
        var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider(Now));
        IMongoDatabase database = factory.Services.GetRequiredService<IMongoDatabase>();
        await InsertNamedAsync(database, "centers", CenterId, "FitLife Center 1").ConfigureAwait(false);
        await InsertNamedAsync(database, "classTypes", ClassTypeId, "Yoga").ConfigureAwait(false);
        return factory;
    }

    private static Task InsertNamedAsync(IMongoDatabase database, string collection, Guid id, string name)
    {
        return database.GetCollection<BsonDocument>(collection)
            .InsertOneAsync(new BsonDocument { { "_id", new BsonBinaryData(id, GuidRepresentation.Standard) }, { "Name", name } });
    }
}
