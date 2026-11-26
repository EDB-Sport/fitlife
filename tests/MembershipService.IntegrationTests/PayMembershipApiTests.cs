using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MembershipService.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MembershipService.IntegrationTests;

/// <summary>A member with Betaling mangler pays the missing fee over HTTP: <c>POST /members/me/payment</c> (UC18, #250).</summary>
public sealed class PayMembershipApiTests
{
    private readonly MongoDbFixture _mongo;
    private readonly MembershipFactory _factory;
    private readonly WebApplicationFactory<Program> _app;
    private readonly SwitchablePaymentProvider _payments = new();

    public PayMembershipApiTests()
    {
        // The factory reads the container's port when the first client is created, after StartMongoDb
        _mongo = new MongoDbFixture();
        _factory = new MembershipFactory(_mongo);
        _app = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentProvider>(_payments)));
    }

    [OneTimeSetUp]
    public async Task StartMongoDb()
    {
        await _mongo.StartAsync().ConfigureAwait(false);
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        await _app.DisposeAsync().ConfigureAwait(false);
        await _factory.DisposeAsync().ConfigureAwait(false);
        await _mongo.DisposeAsync().ConfigureAwait(false);
    }

    [SetUp]
    public void AcceptCharges()
    {
        _payments.Paid = true;
    }

    [Test]
    public async Task Pay_WhenPaymentIsMissing_ReturnsTheActiveMembership()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));
        await JoinWithPaymentMissing(client).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await Pay(client).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("membershipStatus").GetString(), Is.EqualTo("Active"));
        Assert.That(body.RootElement.GetProperty("isActive").GetBoolean(), Is.True);
    }

    [Test]
    public async Task Pay_WhenNothingIsOwed_ReturnsConflictWithTheCode()
    {
        // Arrange: the first fee was paid when joining
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));
        using HttpResponseMessage join = await Join(client).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await Pay(client).ConfigureAwait(false);

        // Assert
        await AssertProblem(response, HttpStatusCode.Conflict, "NothingIsOwed").ConfigureAwait(false);
    }

    [Test]
    public async Task Pay_WhenTheChargeIsDeclined_ReturnsConflictWithTheCode()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));
        await JoinWithPaymentMissing(client).ConfigureAwait(false);
        _payments.Paid = false;

        // Act
        using HttpResponseMessage response = await Pay(client).ConfigureAwait(false);

        // Assert
        await AssertProblem(response, HttpStatusCode.Conflict, "ChargeDeclined").ConfigureAwait(false);
        using HttpResponseMessage mine = await client.GetAsync(new Uri("/members/me", UriKind.Relative)).ConfigureAwait(false);
        using JsonDocument body = await Body(mine).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("membershipStatus").GetString(), Is.EqualTo("PaymentMissing"));
    }

    [Test]
    public async Task Pay_WhenTheAccountHasNotJoined_ReturnsNotFound()
    {
        // Arrange
        using HttpClient client = ClientFor(TestTokens.For(TestTokens.NewAccountId()));

        // Act
        using HttpResponseMessage response = await Pay(client).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Pay_WithoutAToken_ReturnsUnauthorized()
    {
        // Arrange
        using HttpClient client = _app.CreateClient();

        // Act
        using HttpResponseMessage response = await Pay(client).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.That(response.StatusCode, Is.EqualTo(status));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        using JsonDocument body = await Body(response).ConfigureAwait(false);
        Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo(code));
    }

    // A declined first charge leaves the new membership with Betaling mangler
    private async Task JoinWithPaymentMissing(HttpClient client)
    {
        _payments.Paid = false;
        using HttpResponseMessage join = await Join(client).ConfigureAwait(false);
        Assert.That(join.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        _payments.Paid = true;
    }

    private HttpClient ClientFor(string token)
    {
        HttpClient client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Task<HttpResponseMessage> Join(HttpClient client)
    {
        return client.PostAsync(new Uri("/members", UriKind.Relative), content: null);
    }

    private static Task<HttpResponseMessage> Pay(HttpClient client)
    {
        return client.PostAsync(new Uri("/members/me/payment", UriKind.Relative), content: null);
    }

    private static async Task<JsonDocument> Body(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonDocument.Parse(json);
    }

    private sealed class SwitchablePaymentProvider : IPaymentProvider
    {
        public bool Paid { get; set; } = true;

        public Task<bool> ChargeMembershipFeeAsync(Guid memberId, decimal amountDkk, CancellationToken cancellationToken)
        {
            return Task.FromResult(Paid);
        }
    }
}
