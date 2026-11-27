using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookingService.Api;
using FitLife.Authentication;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>
/// Booking checks the token from Identity (#369): the class schedule is open, and only the role Medlem reserves, cancels and joins
/// the waitlist. A class session that doesn't exist answers 404, so a 404 shows the request got past the token check.
/// A reservation without a token is tested in <see cref="ReserveSeatApiTests"/>.
/// </summary>
public sealed class AuthorizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task PostReservation_WithATokenSignedWithAnotherKey_Returns401()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(Guid.NewGuid(), FitLifeRoles.Medlem, TestTokens.OtherKey));

        // Act
        using HttpResponseMessage response = await client.PostAsync(UnknownReservations(), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PostReservation_WithAnExpiredToken_Returns401()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Expired(Guid.NewGuid()));

        // Act
        using HttpResponseMessage response = await client.PostAsync(UnknownReservations(), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [TestCase(FitLifeRoles.Medlem, HttpStatusCode.NotFound)]
    [TestCase(FitLifeRoles.Traener, HttpStatusCode.Forbidden)]
    [TestCase(FitLifeRoles.Admin, HttpStatusCode.Forbidden)]
    public async Task PostReservation_WithEachRole_LetsInMedlemOnly(string role, HttpStatusCode expected)
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = ClientWith(factory, role);

        // Act
        using HttpResponseMessage response = await client.PostAsync(UnknownReservations(), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(expected));
    }

    [TestCase(FitLifeRoles.Medlem, HttpStatusCode.NotFound)]
    [TestCase(FitLifeRoles.Traener, HttpStatusCode.Forbidden)]
    [TestCase(FitLifeRoles.Admin, HttpStatusCode.Forbidden)]
    public async Task DeleteReservation_WithEachRole_LetsInMedlemOnly(string role, HttpStatusCode expected)
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = ClientWith(factory, role);

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(UnknownReservation()).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(expected));
    }

    [TestCase(FitLifeRoles.Medlem, HttpStatusCode.NotFound)]
    [TestCase(FitLifeRoles.Traener, HttpStatusCode.Forbidden)]
    [TestCase(FitLifeRoles.Admin, HttpStatusCode.Forbidden)]
    public async Task PostWaitlist_WithEachRole_LetsInMedlemOnly(string role, HttpStatusCode expected)
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = ClientWith(factory, role);

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(UnknownWaitlist(), new WaitlistEntryRequest(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(expected));
    }

    [Test]
    public async Task DeleteReservation_WithoutAToken_Returns401()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = ClientWithoutAToken(factory);

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(UnknownReservation()).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PostWaitlist_WithoutAToken_Returns401()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = ClientWithoutAToken(factory);

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(UnknownWaitlist(), new WaitlistEntryRequest(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetClassSessions_WithoutAToken_Returns200()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = ClientWithoutAToken(factory);

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/class-sessions", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static Uri UnknownReservations()
    {
        return new Uri($"/class-sessions/{Guid.NewGuid()}/reservations", UriKind.Relative);
    }

    private static Uri UnknownReservation()
    {
        return new Uri($"/class-sessions/{Guid.NewGuid()}/reservations/mine", UriKind.Relative);
    }

    private static Uri UnknownWaitlist()
    {
        return new Uri($"/class-sessions/{Guid.NewGuid()}/waitlist", UriKind.Relative);
    }

    private static HttpClient ClientWith(BookingApiFactory factory, string role)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(Guid.NewGuid(), role));
        return client;
    }

    /// <summary>Every client of the factory logs in as a member, so a test without a token clears the header.</summary>
    private static HttpClient ClientWithoutAToken(BookingApiFactory factory)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }

    private static BookingApiFactory Factory()
    {
        return new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider(Now));
    }
}
