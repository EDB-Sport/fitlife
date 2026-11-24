using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MembershipService.IntegrationTests;

/// <summary>Joining with a token, and the lookup by account id that other services use (#170).</summary>
public sealed class MemberApiTests
{
    private readonly MongoDbFixture _mongo;
    private readonly MembershipFactory _factory;

    public MemberApiTests()
    {
        // The factory reads the container's port when the first client is created, after StartMongoDb
        _mongo = new MongoDbFixture();
        _factory = new MembershipFactory(_mongo);
    }

    [OneTimeSetUp]
    public async Task StartMongoDb()
    {
        await _mongo.StartAsync().ConfigureAwait(false);
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        await _factory.DisposeAsync().ConfigureAwait(false);
        await _mongo.DisposeAsync().ConfigureAwait(false);
    }

    [Test]
    public async Task Join_WithAToken_CreatesAnActiveMember()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));

        // Act
        using HttpResponseMessage response = await Join(client).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("membershipStatus").GetString(), Is.EqualTo("Active"));
        Assert.That(body.RootElement.GetProperty("memberId").GetGuid(), Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task Join_WhenAccountIsAlreadyAMember_IsRejected()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));
        using HttpResponseMessage first = await Join(client).ConfigureAwait(false);

        // Act
        using HttpResponseMessage second = await Join(client).ConfigureAwait(false);

        // Assert
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Join_WithAToken_PointsAtTheLookupForTheAccount()
    {
        // Arrange
        string accountId = TestTokens.NewAccountId();
        using HttpClient client = ClientFor(TestTokens.For(accountId));

        // Act
        using HttpResponseMessage response = await Join(client).ConfigureAwait(false);

        // Assert
        Assert.That(response.Headers.Location?.ToString(), Is.EqualTo($"/members/by-account/{accountId}"));
    }

    [Test]
    public async Task Join_WhenAccountIsAlreadyAMember_AnswersWithProblemDetails()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));
        using HttpResponseMessage first = await Join(client).ConfigureAwait(false);

        // Act
        using HttpResponseMessage second = await Join(client).ConfigureAwait(false);

        // Assert
        Assert.That(second.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        using JsonDocument body = await Body(second).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("status").GetInt32(), Is.EqualTo(409));
        Assert.That(body.RootElement.GetProperty("title").GetString(), Is.EqualTo("The account is already a member"));
    }

    [Test]
    public async Task Join_WithATokenWithoutAnAccountId_ReturnsUnauthorized()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.WithoutAccountId());

        // Act
        using HttpResponseMessage response = await Join(client).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Join_WithoutAToken_ReturnsUnauthorized()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        using HttpResponseMessage response = await Join(client).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Join_WithATokenSignedWithAnotherKey_ReturnsUnauthorized()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId(), TestTokens.OtherKey));

        // Act
        using HttpResponseMessage response = await Join(client).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetByAccount_AfterJoining_ReturnsTheMemberIdAndActive()
    {
        // Arrange
        string accountId = TestTokens.NewAccountId();
        using HttpClient client = ClientFor(TestTokens.For(accountId));
        using HttpResponseMessage joined = await Join(client).ConfigureAwait(false);
        using JsonDocument joinedBody = await Body(joined).ConfigureAwait(false);
        Guid memberId = joinedBody.RootElement.GetProperty("memberId").GetGuid();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri($"/members/by-account/{accountId}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("memberId").GetGuid(), Is.EqualTo(memberId));
        Assert.That(body.RootElement.GetProperty("isActive").GetBoolean(), Is.True);
    }

    [Test]
    public async Task GetByAccount_ForAnUnknownAccount_ReturnsNotFound()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri($"/members/by-account/{TestTokens.NewAccountId()}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetByAccount_WithoutAToken_ReturnsUnauthorized()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri($"/members/by-account/{TestTokens.NewAccountId()}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private HttpClient ClientFor(string token)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Task<HttpResponseMessage> Join(HttpClient client)
    {
        return client.PostAsync(new Uri("/members", UriKind.Relative), content: null);
    }

    private static async Task<JsonDocument> Body(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonDocument.Parse(json);
    }
}
