using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace MembershipService.IntegrationTests;

/// <summary>
/// <c>PUT</c> and <c>DELETE /members/me/pause</c> (UC6, #248), and the status the lookups give inside a pause period.
/// The clock stands still at the moment the tests start, so "today" can't change during a test; it is the real time,
/// so the test tokens stay valid.
/// </summary>
public sealed class PauseApiTests
{
    private static readonly Uri PauseUri = new("/members/me/pause", UriKind.Relative);

    private readonly MongoDbFixture _mongo;
    private readonly MembershipFactory _factory;
    private readonly FakeTimeProvider _clock = new(TimeProvider.System.GetUtcNow());
    private WebApplicationFactory<Program>? _app;

    public PauseApiTests()
    {
        _mongo = new MongoDbFixture();
        _factory = new MembershipFactory(_mongo);
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    private WebApplicationFactory<Program> App => _app ?? throw new InvalidOperationException("MongoDB isn't started");

    [OneTimeSetUp]
    public async Task StartMongoDb()
    {
        await _mongo.StartAsync().ConfigureAwait(false);
        _app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(_clock)));
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        await _factory.DisposeAsync().ConfigureAwait(false);
        await _mongo.DisposeAsync().ConfigureAwait(false);
    }

    [Test]
    public async Task Pause_WhenActive_ReturnsTheMembershipWithThePausePeriod()
    {
        // Arrange
        using HttpClient client = await JoinedClient().ConfigureAwait(false);
        DateOnly from = Today.AddDays(7);
        DateOnly to = Today.AddDays(37);

        // Act
        using HttpResponseMessage response = await Pause(client, Iso(from), Iso(to)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("membershipStatus").GetString(), Is.EqualTo("Active"));
        Assert.That(body.RootElement.GetProperty("isActive").GetBoolean(), Is.True);
        Assert.That(body.RootElement.GetProperty("membershipType").GetString(), Is.EqualTo("Standard"));
        Assert.That(body.RootElement.GetProperty("pausedFrom").GetString(), Is.EqualTo(Iso(from)));
        Assert.That(body.RootElement.GetProperty("pausedTo").GetString(), Is.EqualTo(Iso(to)));
    }

    [Test]
    public async Task GetByAccount_InsideThePausePeriod_IsNotActive()
    {
        // Arrange
        string accountId = TestTokens.NewAccountId();
        using HttpClient client = await JoinedClient(accountId).ConfigureAwait(false);
        using HttpResponseMessage paused = await Pause(client, Iso(Today), Iso(Today.AddDays(30))).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri($"/members/by-account/{accountId}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(paused.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("membershipStatus").GetString(), Is.EqualTo("Paused"));
        Assert.That(body.RootElement.GetProperty("isActive").GetBoolean(), Is.False);
    }

    [Test]
    public async Task GetMine_InsideThePausePeriod_IsPaused()
    {
        // Arrange
        using HttpClient client = await JoinedClient().ConfigureAwait(false);
        using HttpResponseMessage paused = await Pause(client, Iso(Today), Iso(Today.AddDays(30))).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/members/me", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("membershipStatus").GetString(), Is.EqualTo("Paused"));
        Assert.That(body.RootElement.GetProperty("isActive").GetBoolean(), Is.False);
    }

    [Test]
    public async Task Pause_StartingBeforeToday_Returns409WithItsCode()
    {
        // Arrange
        using HttpClient client = await JoinedClient().ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await Pause(client, Iso(Today.AddDays(-1)), Iso(Today.AddDays(30))).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("PauseStartsInThePast"));
    }

    [Test]
    public async Task Pause_EndingOnItsStart_Returns409WithItsCode()
    {
        // Arrange
        using HttpClient client = await JoinedClient().ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await Pause(client, Iso(Today.AddDays(7)), Iso(Today.AddDays(7))).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("PauseEndsBeforeItStarts"));
    }

    [TestCase("not-a-date")]
    [TestCase("2026-13-01")]
    public async Task Pause_WithAMalformedDate_Returns400(string from)
    {
        // Arrange
        using HttpClient client = await JoinedClient().ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await Pause(client, from, Iso(Today.AddDays(30))).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Pause_WithoutADate_Returns400()
    {
        // Arrange
        using HttpClient client = await JoinedClient().ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.PutAsJsonAsync(PauseUri, new { to = Iso(Today.AddDays(30)) }).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Pause_WhenTheAccountHasNotJoined_Returns404()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));

        // Act
        using HttpResponseMessage response = await Pause(client, Iso(Today.AddDays(7)), Iso(Today.AddDays(30))).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Pause_WithoutAToken_Returns401()
    {
        // Arrange
        using HttpClient client = App.CreateClient();

        // Act
        using HttpResponseMessage response = await Pause(client, Iso(Today.AddDays(7)), Iso(Today.AddDays(30))).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task EndPause_WhilePaused_ReturnsAnActiveMembershipWithoutAPause()
    {
        // Arrange
        using HttpClient client = await JoinedClient().ConfigureAwait(false);
        using HttpResponseMessage paused = await Pause(client, Iso(Today), Iso(Today.AddDays(30))).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(PauseUri).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("membershipStatus").GetString(), Is.EqualTo("Active"));
        Assert.That(body.RootElement.GetProperty("isActive").GetBoolean(), Is.True);
        Assert.That(body.RootElement.GetProperty("pausedFrom").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task EndPause_WithNoPause_Returns409NoPauseToEnd()
    {
        // Arrange
        using HttpClient client = await JoinedClient().ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(PauseUri).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("NoPauseToEnd"));
    }

    [Test]
    public async Task EndPause_WhenTheAccountHasNotJoined_Returns404()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(PauseUri).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task EndPause_WithoutAToken_Returns401()
    {
        // Arrange
        using HttpClient client = App.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(PauseUri).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private static string Iso(DateOnly day)
    {
        return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static Task<HttpResponseMessage> Pause(HttpClient client, string from, string to)
    {
        return client.PutAsJsonAsync(PauseUri, new { from, to });
    }

    private static async Task<JsonDocument> Body(HttpResponseMessage response)
    {
        Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
    }

    private HttpClient ClientFor(string token)
    {
        HttpClient client = App.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>A client for a new account that has joined, with an active membership (the payment provider accepts).</summary>
    private async Task<HttpClient> JoinedClient(string? accountId = null)
    {
        HttpClient client = ClientFor(TestTokens.For(accountId ?? TestTokens.NewAccountId()));
        using HttpResponseMessage joined = await client.PostAsync(new Uri("/members", UriKind.Relative), content: null).ConfigureAwait(false);
        Assert.That(joined.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return client;
    }
}
