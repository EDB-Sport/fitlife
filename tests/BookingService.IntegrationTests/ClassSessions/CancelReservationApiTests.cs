using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookingService.Api;
using BookingService.Application;
using BookingService.Domain;
using FitLife.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>A member cancels their own reservation with their token; the route has no member id (#262).</summary>
public sealed class CancelReservationApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task DeleteMyReservation_WithoutAToken_ReturnsUnauthorized()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory, reservedBy: Guid.NewGuid()).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(MyReservationOn(classSession)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task DeleteMyReservation_WithoutTheRoleMedlem_Returns403()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        var memberId = Guid.NewGuid();
        factory.Membership.Answer = MemberLookup.Found(memberId, isActive: true);
        ClassSession classSession = await StoredAsync(factory, reservedBy: memberId).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(Guid.NewGuid(), FitLifeRoles.Traener));

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(MyReservationOn(classSession)).ConfigureAwait(false);

        // Assert: the token's account is a member with a reservation here, so only the role stops the cancellation
        int? freeSeats = await FreeSeatsAsync(client, classSession).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(freeSeats, Is.Zero, "the reservation is kept");
        });
    }

    [Test]
    public async Task DeleteMyReservation_WhenIHaveAReservation_ReturnsNoContentAndFreesTheSeat()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        var memberId = Guid.NewGuid();
        factory.Membership.Answer = MemberLookup.Found(memberId, isActive: true);
        ClassSession classSession = await StoredAsync(factory, reservedBy: memberId).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(MyReservationOn(classSession)).ConfigureAwait(false);

        // Assert
        int? freeSeats = await FreeSeatsAsync(client, classSession).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(freeSeats, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task DeleteMyReservation_AsksMembershipWithTheTokensAccountIdAndTheTokenUnchanged()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory, reservedBy: Guid.NewGuid()).ConfigureAwait(false);
        var accountId = Guid.NewGuid();
        string token = TestTokens.For(accountId);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(MyReservationOn(classSession)).ConfigureAwait(false);

        // Assert: the account id is the token's sub claim, and the token goes on without its "Bearer " prefix
        Assert.That((factory.Membership.AskedAccountId, factory.Membership.AskedBearerToken), Is.EqualTo(((Guid?)accountId, token)));
    }

    [Test]
    public async Task DeleteMyReservation_WhenIHaveNone_ReturnsNotFound()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory, reservedBy: null).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(MyReservationOn(classSession)).ConfigureAwait(false);

        // Assert
        Assert.That(await StatusAndCodeAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.NotFound, "NoReservation")));
    }

    [Test]
    public async Task DeleteMyReservation_WhenAnotherMemberHasTheReservation_ReturnsNotFoundAndKeepsIt()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        factory.Membership.Answer = MemberLookup.Found(Guid.NewGuid(), isActive: true);
        ClassSession classSession = await StoredAsync(factory, reservedBy: Guid.NewGuid()).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(MyReservationOn(classSession)).ConfigureAwait(false);

        // Assert
        (HttpStatusCode Status, string? Code) problem = await StatusAndCodeAsync(response).ConfigureAwait(false);
        int? freeSeats = await FreeSeatsAsync(client, classSession).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.EqualTo((HttpStatusCode.NotFound, "NoReservation")));
            Assert.That(freeSeats, Is.Zero, "the other member keeps the seat");
        });
    }

    [Test]
    public async Task DeleteMyReservation_OnAnUnknownClassSession_ReturnsNotFound()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(new Uri($"/class-sessions/{Guid.NewGuid()}/reservations/mine", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(await StatusAndCodeAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.NotFound, "ClassSessionNotFound")));
    }

    [Test]
    public async Task DeleteMyReservation_WhenMembershipIsUnavailable_Returns503WithCode()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        factory.Membership.Answer = MemberLookup.Unavailable;
        ClassSession classSession = await StoredAsync(factory, reservedBy: Guid.NewGuid()).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(MyReservationOn(classSession)).ConfigureAwait(false);

        // Assert
        Assert.That(await StatusAndCodeAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.ServiceUnavailable, "MembershipUnavailable")));
    }

    private static async Task<ClassSession> StoredAsync(BookingApiFactory factory, Guid? reservedBy)
    {
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(1), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        if (reservedBy is Guid memberId)
        {
            classSession.Reserve(memberId, factory.Clock);
        }

        await factory.AddAsync(classSession).ConfigureAwait(false);
        return classSession;
    }

    private static Uri MyReservationOn(ClassSession classSession)
    {
        return new Uri($"/class-sessions/{classSession.Id}/reservations/mine", UriKind.Relative);
    }

    private static async Task<int?> FreeSeatsAsync(HttpClient client, ClassSession classSession)
    {
        ClassSessionResponse[]? schedule = await client.GetFromJsonAsync<ClassSessionResponse[]>(new Uri($"/class-sessions?centerId={classSession.CenterId}", UriKind.Relative)).ConfigureAwait(false);
        return schedule?.Single(found => found.Id == classSession.Id).FreeSeats;
    }

    private static async Task<(HttpStatusCode Status, string? Code)> StatusAndCodeAsync(HttpResponseMessage response)
    {
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>().ConfigureAwait(false);
        object? code = null;
        problem?.Extensions.TryGetValue("code", out code);
        return (response.StatusCode, code?.ToString());
    }

    private static BookingApiFactory Factory()
    {
        return new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider(Now));
    }
}
