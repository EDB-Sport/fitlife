using System.Net;
using System.Net.Http.Json;
using BookingService.Api;
using BookingService.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>Reserve, cancel and join the waitlist over HTTP, stored in MongoDB (#171).</summary>
public sealed class ClassSessionApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private string _database = "";

    [SetUp]
    public void UseAFreshDatabase()
    {
        _database = $"booking-{Guid.NewGuid():N}";
    }

    [Test]
    public async Task GetClassSessions_ForACentre_ListsItsClassSessionsWithTheirFreeSeats()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        var centerId = Guid.NewGuid();
        ClassSession halfFull = ClassSessionAt(centerId, capacity: 2, Now.AddDays(1));
        halfFull.Reserve(Guid.NewGuid(), factory.Clock);
        await factory.AddAsync(halfFull).ConfigureAwait(false);
        await factory.AddAsync(ClassSessionAt(Guid.NewGuid(), capacity: 2, Now.AddDays(1))).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        ClassSessionResponse[]? classSessions = await client.GetFromJsonAsync<ClassSessionResponse[]>(new Uri($"/class-sessions?centerId={centerId}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(classSessions, Is.EqualTo(new[] { new ClassSessionResponse(halfFull.Id, centerId, "", halfFull.ClassTypeId, "", "Mette", halfFull.StartTime, DurationMinutes: 60, Capacity: 2, FreeSeats: 1, IsCancelled: false) }));
    }

    [Test]
    public async Task GetClassSessions_WithACancelledClassSession_ListsItAsCancelledWithNoFreeSeats()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        var centerId = Guid.NewGuid();
        ClassSession cancelled = ClassSessionAt(centerId, capacity: 2, Now.AddDays(1));
        cancelled.Cancel();
        await factory.AddAsync(cancelled).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        ClassSessionResponse[]? classSessions = await client.GetFromJsonAsync<ClassSessionResponse[]>(new Uri($"/class-sessions?centerId={centerId}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(classSessions, Is.EqualTo(new[] { new ClassSessionResponse(cancelled.Id, centerId, "", cancelled.ClassTypeId, "", "Mette", cancelled.StartTime, DurationMinutes: 60, Capacity: 2, FreeSeats: 0, IsCancelled: true) }));
    }

    [Test]
    public async Task PostReservation_WithAFreeSeat_Returns201AndTheReservationSurvivesARestart()
    {
        // Arrange
        var centerId = Guid.NewGuid();
        ClassSession classSession = ClassSessionAt(centerId, capacity: 1, Now.AddDays(1));
        var memberId = Guid.NewGuid();
        HttpStatusCode status;
        Uri? location;
        using (BookingApiFactory factory = Factory())
        {
            await factory.AddAsync(classSession).ConfigureAwait(false);
            using HttpClient client = factory.CreateClient();

            // Act
            using HttpResponseMessage response = await client.PostAsJsonAsync(ReservationsOf(classSession), new ReservationRequest(memberId)).ConfigureAwait(false);
            status = response.StatusCode;
            location = response.Headers.Location;
        }

        // Assert
        using BookingApiFactory restarted = Factory();
        using HttpClient restartedClient = restarted.CreateClient();
        ClassSessionResponse[]? classSessions = await restartedClient.GetFromJsonAsync<ClassSessionResponse[]>(new Uri($"/class-sessions?centerId={centerId}", UriKind.Relative)).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(location, Is.Null, "no GET exists for a single reservation, so there is nothing to point at");
            Assert.That(classSessions?.Single().FreeSeats, Is.Zero);
        });
    }

    [Test]
    public async Task PostReservation_OnAFullClassSession_Returns409ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(1));
        classSession.Reserve(Guid.NewGuid(), factory.Clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ReservationsOf(classSession), new ReservationRequest(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", (int?)409)));
    }

    [Test]
    public async Task PostReservation_WhenTheMemberAlreadyHasAReservation_Returns409ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 2, Now.AddDays(1));
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, factory.Clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ReservationsOf(classSession), new ReservationRequest(memberId)).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", (int?)409)));
    }

    [Test]
    public async Task PostReservation_MoreThanFourWeeksAhead_Returns422ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(29));
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ReservationsOf(classSession), new ReservationRequest(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.UnprocessableEntity, "application/problem+json", (int?)422)));
    }

    [Test]
    public async Task PostReservation_OnAnUnknownClassSession_Returns404ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(new Uri($"/class-sessions/{Guid.NewGuid()}/reservations", UriKind.Relative), new ReservationRequest(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.NotFound, "application/problem+json", (int?)404)));
    }

    [Test]
    public async Task DeleteReservation_Returns204AndGivesTheSeatToTheFirstWaitlistEntry()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        var centerId = Guid.NewGuid();
        ClassSession classSession = ClassSessionAt(centerId, capacity: 1, Now.AddDays(1));
        var memberId = Guid.NewGuid();
        var waitingMemberId = Guid.NewGuid();
        classSession.Reserve(memberId, factory.Clock);
        classSession.JoinWaitlist(waitingMemberId, factory.Clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(new Uri($"/class-sessions/{classSession.Id}/reservations/{memberId}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        using HttpResponseMessage again = await client.PostAsJsonAsync(ReservationsOf(classSession), new ReservationRequest(waitingMemberId)).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), "the waiting member already has the freed seat");
        });
    }

    [Test]
    public async Task DeleteReservation_WhenTheMemberHasNoReservation_Returns404ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(1));
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(new Uri($"/class-sessions/{classSession.Id}/reservations/{Guid.NewGuid()}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.NotFound, "application/problem+json", (int?)404)));
    }

    [Test]
    public async Task PostWaitlist_OnAFullClassSession_Returns201()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(1));
        classSession.Reserve(Guid.NewGuid(), factory.Clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();
        var memberId = Guid.NewGuid();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(WaitlistOf(classSession), new WaitlistEntryRequest(memberId)).ConfigureAwait(false);

        // Assert
        WaitlistEntryResponse? waitlistEntry = await response.Content.ReadFromJsonAsync<WaitlistEntryResponse>().ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(waitlistEntry, Is.EqualTo(new WaitlistEntryResponse(classSession.Id, memberId)));
            Assert.That(response.Headers.Location, Is.Null, "no GET exists for a single waitlist entry, so there is nothing to point at");
        });
    }

    [Test]
    public async Task PostWaitlist_WithFreeSeats_Returns409ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(1));
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(WaitlistOf(classSession), new WaitlistEntryRequest(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", (int?)409)));
    }

    [Test]
    public async Task PostWaitlist_WhenTheMemberAlreadyHasAReservation_Returns409ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(1));
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, factory.Clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(WaitlistOf(classSession), new WaitlistEntryRequest(memberId)).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", (int?)409)));
    }

    [Test]
    public async Task PostWaitlist_WhenTheMemberIsAlreadyOnTheWaitlist_Returns409ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(1));
        var memberId = Guid.NewGuid();
        classSession.Reserve(Guid.NewGuid(), factory.Clock);
        classSession.JoinWaitlist(memberId, factory.Clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(WaitlistOf(classSession), new WaitlistEntryRequest(memberId)).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", (int?)409)));
    }

    [Test]
    public async Task PostWaitlist_OnACancelledClassSession_Returns409ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(1));
        classSession.Cancel();
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(WaitlistOf(classSession), new WaitlistEntryRequest(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", (int?)409)));
    }

    [Test]
    public async Task PostWaitlist_AfterTheWaitlistHasClosed_Returns409ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddHours(1));
        classSession.Reserve(Guid.NewGuid(), factory.Clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(WaitlistOf(classSession), new WaitlistEntryRequest(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", (int?)409)));
    }

    [Test]
    public async Task PostReservation_OnACancelledClassSession_Returns409ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(1));
        classSession.Cancel();
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ReservationsOf(classSession), new ReservationRequest(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", (int?)409)));
    }

    [Test]
    public async Task PostReservation_WhenAnotherRequestSavedFirst_Returns409ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory(everySaveIsStale: true);
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(1));
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(ReservationsOf(classSession), new ReservationRequest(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", (int?)409)));
    }

    [Test]
    public async Task DeleteReservation_WhenAnotherRequestSavedFirst_Returns409ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory(everySaveIsStale: true);
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(1));
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, factory.Clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(new Uri($"/class-sessions/{classSession.Id}/reservations/{memberId}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", (int?)409)));
    }

    [Test]
    public async Task PostWaitlist_WhenAnotherRequestSavedFirst_Returns409ProblemDetails()
    {
        // Arrange
        using BookingApiFactory factory = Factory(everySaveIsStale: true);
        ClassSession classSession = ClassSessionAt(Guid.NewGuid(), capacity: 1, Now.AddDays(1));
        classSession.Reserve(Guid.NewGuid(), factory.Clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(WaitlistOf(classSession), new WaitlistEntryRequest(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That(await ProblemOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", (int?)409)));
    }

    [Test]
    public async Task Start_WithSeedEnabled_LoadsTheDemoClassSessions()
    {
        // Arrange
        using BookingApiFactory factory = Factory(seed: true);
        using HttpClient client = factory.CreateClient();
        await factory.SeedFinishedAsync().ConfigureAwait(false);

        // Act
        ClassSessionResponse[]? classSessions = await client.GetFromJsonAsync<ClassSessionResponse[]>(new Uri("/class-sessions", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(classSessions, Is.Not.Empty);
    }

    private static ClassSession ClassSessionAt(Guid centerId, int capacity, DateTimeOffset startTime)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(capacity), startTime, new ClassSessionDetails(centerId, Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
    }

    private static Uri ReservationsOf(ClassSession classSession)
    {
        return new Uri($"/class-sessions/{classSession.Id}/reservations", UriKind.Relative);
    }

    private static Uri WaitlistOf(ClassSession classSession)
    {
        return new Uri($"/class-sessions/{classSession.Id}/waitlist", UriKind.Relative);
    }

    /// <summary>The status code, the media type and the problem-details status of a response.</summary>
    private static async Task<(HttpStatusCode Status, string? MediaType, int? ProblemStatus)> ProblemOfAsync(HttpResponseMessage response)
    {
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>().ConfigureAwait(false);
        return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, problem?.Status);
    }

    private BookingApiFactory Factory(bool seed = false, bool everySaveIsStale = false)
    {
        return new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, _database, new FakeTimeProvider(Now), seed, everySaveIsStale);
    }
}
