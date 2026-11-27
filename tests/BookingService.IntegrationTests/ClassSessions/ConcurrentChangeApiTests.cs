using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookingService.Application;
using BookingService.Domain;
using FitLife.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>Requests that change the same class session at the same time (#245).</summary>
public sealed class ConcurrentChangeApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task PostReservation_ByTwoMembersAtTheSameMomentForTheLastSeat_ExactlyOneIsCreated()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = NewClassSession(capacity: 2);
        classSession.Reserve(Guid.NewGuid(), factory.Clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient first = ClientOfANewMember(factory);
        using HttpClient second = ClientOfANewMember(factory);
        var reservations = new Uri($"/class-sessions/{classSession.Id}/reservations", UriKind.Relative);

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(
            first.PostAsync(reservations, content: null),
            second.PostAsync(reservations, content: null)).ConfigureAwait(false);

        // Assert
        HttpStatusCode[] statuses = responses.Select(response => response.StatusCode).ToArray();
        Array.ForEach(responses, response => response.Dispose());
        using IServiceScope scope = factory.Services.CreateScope();
        ClassSession? stored = await scope.ServiceProvider.GetRequiredService<IClassSessionRepository>()
            .GetByIdAsync(classSession.Id, CancellationToken.None)
            .ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(statuses, Is.EquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
            Assert.That(stored?.Reservations, Has.Count.EqualTo(classSession.Capacity.Value));
        });
    }

    [Test]
    public async Task PostReservation_WhenEverySaveIsRefused_Returns409ConcurrentChange()
    {
        // Arrange
        using BookingApiFactory factory = Factory(everySaveIsStale: true);
        ClassSession classSession = NewClassSession(capacity: 1);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsync(new Uri($"/class-sessions/{classSession.Id}/reservations", UriKind.Relative), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(await AnswerOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", "ConcurrentChange")));
    }

    [Test]
    public async Task DeleteReservation_WhenTheSaveIsRefused_Returns409ConcurrentChange()
    {
        // Arrange
        using BookingApiFactory factory = Factory(everySaveIsStale: true);
        ClassSession classSession = NewClassSession(capacity: 1);
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, factory.Clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(new Uri($"/class-sessions/{classSession.Id}/reservations/{memberId}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(await AnswerOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", "ConcurrentChange")));
    }

    [Test]
    public async Task PostCancellation_WhenTheSaveIsRefused_Returns409ConcurrentChange()
    {
        // Arrange
        using BookingApiFactory factory = Factory(everySaveIsStale: true);
        ClassSession classSession = NewClassSession(capacity: 1);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(Guid.NewGuid(), FitLifeRoles.Traener));

        // Act
        using HttpResponseMessage response = await client.PostAsync(new Uri($"/class-sessions/{classSession.Id}/cancellation", UriKind.Relative), content: null).ConfigureAwait(false);

        // Assert
        Assert.That(await AnswerOfAsync(response).ConfigureAwait(false), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", "ConcurrentChange")));
    }

    private static HttpClient ClientOfANewMember(BookingApiFactory factory)
    {
        var accountId = Guid.NewGuid();
        factory.Membership.AnswerByAccount[accountId] = MemberLookup.Found(Guid.NewGuid(), isActive: true);
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(accountId));
        return client;
    }

    private static ClassSession NewClassSession(int capacity)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(capacity), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
    }

    private static async Task<(HttpStatusCode Status, string? ContentType, string? Code)> AnswerOfAsync(HttpResponseMessage response)
    {
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>().ConfigureAwait(false);
        object? code = null;
        problem?.Extensions.TryGetValue("code", out code);
        return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, code?.ToString());
    }

    private static BookingApiFactory Factory(bool everySaveIsStale = false)
    {
        return new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider(Now), everySaveIsStale: everySaveIsStale);
    }
}
