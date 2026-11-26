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

/// <summary>A member reserves a seat with their token: Booking asks Membership who the member is (#260).</summary>
public sealed class ReserveSeatApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task PostReservation_WithoutAToken_Returns401()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory, capacity: 1).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        // Act
        using HttpResponseMessage response = await client.PostAsync(ReservationsOf(classSession), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PostReservation_WithoutTheRoleMedlem_Returns403()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory, capacity: 1).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(Guid.NewGuid(), FitLifeRoles.Traener));

        // Act
        using HttpResponseMessage response = await client.PostAsync(ReservationsOf(classSession), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task PostReservation_ForAnActiveMember_Returns201WithTheMemberId()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        var memberId = Guid.NewGuid();
        factory.Membership.Answer = MemberLookup.Found(memberId, isActive: true);
        ClassSession classSession = await StoredAsync(factory, capacity: 1).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsync(ReservationsOf(classSession), content: null).ConfigureAwait(false);

        // Assert
        ReservationResponse? reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>().ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(reservation, Is.EqualTo(new ReservationResponse(classSession.Id, memberId)));
        });
    }

    [Test]
    public async Task PostReservation_OnAFullClassSession_Returns409ClassSessionFull()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory, capacity: 1, reservedBy: Guid.NewGuid()).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsync(ReservationsOf(classSession), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(await StatusAndCodeAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "ClassSessionFull")));
    }

    [Test]
    public async Task PostReservation_WhenTheMembershipIsNotActive_Returns403WithCode()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        factory.Membership.Answer = MemberLookup.Found(Guid.NewGuid(), isActive: false);
        ClassSession classSession = await StoredAsync(factory, capacity: 1).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsync(ReservationsOf(classSession), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(await StatusAndCodeAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Forbidden, "MembershipNotActive")));
    }

    [Test]
    public async Task PostReservation_WhenMembershipIsUnavailable_Returns503()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        factory.Membership.Answer = MemberLookup.Unavailable;
        ClassSession classSession = await StoredAsync(factory, capacity: 1).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsync(ReservationsOf(classSession), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(await StatusAndCodeAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.ServiceUnavailable, "MembershipUnavailable")));
    }

    private static async Task<ClassSession> StoredAsync(BookingApiFactory factory, int capacity, Guid? reservedBy = null)
    {
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(capacity), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        if (reservedBy is Guid memberId)
        {
            classSession.Reserve(memberId, factory.Clock);
        }

        await factory.AddAsync(classSession).ConfigureAwait(false);
        return classSession;
    }

    private static Uri ReservationsOf(ClassSession classSession)
    {
        return new Uri($"/class-sessions/{classSession.Id}/reservations", UriKind.Relative);
    }

    /// <summary>The status code and the problem details' <c>code</c>.</summary>
    private static async Task<(HttpStatusCode Status, string? Code)> StatusAndCodeAsync(HttpResponseMessage response)
    {
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>().ConfigureAwait(false);
        string? code = problem is not null && problem.Extensions.TryGetValue("code", out object? value) ? value?.ToString() : null;
        return (response.StatusCode, code);
    }

    private static BookingApiFactory Factory()
    {
        return new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider(Now));
    }
}
