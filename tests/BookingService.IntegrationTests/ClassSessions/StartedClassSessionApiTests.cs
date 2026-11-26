using System.Net;
using System.Net.Http.Json;
using BookingService.Api;
using BookingService.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>Reserving a seat on a class session that has started answers 409 (#312).</summary>
public sealed class StartedClassSessionApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task PostReservation_AfterTheClassSessionHasStarted_Returns409ProblemDetails()
    {
        // Arrange
        var clock = new FakeTimeProvider(Now);
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", clock);
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(1), Now.AddMinutes(-5), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsync(new Uri($"/class-sessions/{classSession.Id}/reservations", UriKind.Relative), content: null).ConfigureAwait(false);

        // Assert
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>().ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(problem?.Title, Is.EqualTo("The class session has started."));
        });
    }
}
