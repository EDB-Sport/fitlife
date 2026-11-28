using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace MembershipService.IntegrationTests;

/// <summary>
/// <c>POST /members/me/cancellation</c> (UC8, #249): one call, no body, and what <c>GET /members/me</c> shows afterwards.
/// The clock stands still at a fixed moment in the real month, so the last training day is known; the test tokens stay valid.
/// </summary>
public sealed class CancellationApiTests
{
    private static readonly Uri CancellationUri = new("/members/me/cancellation", UriKind.Relative);

    private readonly MongoDbFixture _mongo;
    private readonly MembershipFactory _factory;
    private readonly FakeTimeProvider _clock = new(TimeProvider.System.GetUtcNow());
    private WebApplicationFactory<Program>? _app;

    public CancellationApiTests()
    {
        _mongo = new MongoDbFixture();
        _factory = new MembershipFactory(_mongo);
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    // The last day of the month after today's month
    private DateOnly LastTrainingDay => new DateOnly(Today.Year, Today.Month, 1).AddMonths(2).AddDays(-1);

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
    public async Task Cancel_WhenActive_Returns200WithTheLastTrainingDay()
    {
        // Arrange
        using HttpClient client = await JoinedClient().ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.PostAsync(CancellationUri, content: null).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("lastTrainingDay").GetString(), Is.EqualTo(Iso(LastTrainingDay)));
    }

    [Test]
    public async Task GetMine_AfterCancelling_ShowsTheStatusAndTheLastTrainingDay()
    {
        // Arrange
        using HttpClient client = await JoinedClient().ConfigureAwait(false);
        using HttpResponseMessage cancelled = await client.PostAsync(CancellationUri, content: null).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/members/me", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(cancelled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("membershipStatus").GetString(), Is.EqualTo("Cancelled"));
        Assert.That(body.RootElement.GetProperty("isActive").GetBoolean(), Is.False);
        Assert.That(body.RootElement.GetProperty("lastTrainingDay").GetString(), Is.EqualTo(Iso(LastTrainingDay)));
    }

    [Test]
    public async Task Cancel_WhenAlreadyCancelled_Returns409MembershipAlreadyCancelled()
    {
        // Arrange
        using HttpClient client = await JoinedClient().ConfigureAwait(false);
        using HttpResponseMessage first = await client.PostAsync(CancellationUri, content: null).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.PostAsync(CancellationUri, content: null).ConfigureAwait(false);

        // Assert
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("MembershipAlreadyCancelled"));
    }

    [Test]
    public async Task Cancel_WhenTheAccountHasNotJoined_Returns404()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));

        // Act
        using HttpResponseMessage response = await client.PostAsync(CancellationUri, content: null).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Cancel_WithoutAToken_Returns401()
    {
        // Arrange
        using HttpClient client = App.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsync(CancellationUri, content: null).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private static string Iso(DateOnly day)
    {
        return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
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
    private async Task<HttpClient> JoinedClient()
    {
        HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));
        using HttpResponseMessage joined = await client.PostAsync(new Uri("/members", UriKind.Relative), content: null).ConfigureAwait(false);
        Assert.That(joined.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return client;
    }
}
