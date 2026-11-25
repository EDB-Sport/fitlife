using System.Net;
using System.Text;
using System.Text.Json;
using MembershipService.PaymentProvider;

namespace MembershipService.Tests;

/// <summary>
/// The anticorruption layer to the payment provider (#174): the provider's charges API in, "is the fee paid" out.
/// The HTTP calls go to a stub handler, never to Postman.
/// </summary>
public sealed class PaymentProviderClientTests
{
    private static readonly Guid MemberId = Guid.Parse("7d0f6c1a-2b3c-4d5e-8f90-a1b2c3d4e5f6");

    [Test]
    public async Task Charge_WhenTheProviderApproves_IsPaid()
    {
        // Arrange
        using var provider = new StubProvider(HttpStatusCode.Created, """{ "charge_id": "ch_1001", "outcome": "approved" }""");

        // Act
        bool paid = await Charge(provider);

        // Assert
        Assert.That(paid, Is.True);
    }

    [Test]
    public async Task Charge_WhenTheProviderDeclines_IsNotPaid()
    {
        // Arrange
        using var provider = new StubProvider(HttpStatusCode.PaymentRequired, """{ "charge_id": "ch_1002", "outcome": "declined", "decline_code": "insufficient_funds" }""");

        // Act
        bool paid = await Charge(provider);

        // Assert
        Assert.That(paid, Is.False);
    }

    [Test]
    public async Task Charge_WhenTheProviderFails_IsNotPaid()
    {
        // Arrange
        using var provider = new StubProvider(HttpStatusCode.ServiceUnavailable, """{ "error": "service_unavailable" }""");

        // Act
        bool paid = await Charge(provider);

        // Assert
        Assert.That(paid, Is.False);
    }

    [Test]
    public async Task Charge_WhenTheProviderCannotBeReached_IsNotPaid()
    {
        // Arrange
        using var provider = new StubProvider(new HttpRequestException("connection refused"));

        // Act
        bool paid = await Charge(provider);

        // Assert
        Assert.That(paid, Is.False);
    }

    [Test]
    public async Task Charge_WhenTheProviderTimesOut_IsNotPaid()
    {
        // Arrange: HttpClient reports its own timeout as a TaskCanceledException
        using var provider = new StubProvider(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));

        // Act
        bool paid = await Charge(provider);

        // Assert
        Assert.That(paid, Is.False);
    }

    [Test]
    public async Task Charge_WhenTheProviderAnswersWithoutJson_IsNotPaid()
    {
        // Arrange
        using var provider = new StubProvider(HttpStatusCode.OK, "<html>maintenance</html>");

        // Act
        bool paid = await Charge(provider);

        // Assert
        Assert.That(paid, Is.False);
    }

    [Test]
    public async Task Charge_WhenTheProviderAnswersWithoutAnOutcome_IsNotPaid()
    {
        // Arrange
        using var provider = new StubProvider(HttpStatusCode.Created, """{ "charge_id": "ch_1001" }""");

        // Act
        bool paid = await Charge(provider);

        // Assert
        Assert.That(paid, Is.False);
    }

    [Test]
    public async Task Charge_WhenTheOutcomeIsNotAString_IsNotPaid()
    {
        // Arrange
        using var provider = new StubProvider(HttpStatusCode.Created, """{ "charge_id": "ch_1001", "outcome": 1 }""");

        // Act
        bool paid = await Charge(provider);

        // Assert
        Assert.That(paid, Is.False);
    }

    [Test]
    public async Task Charge_WhenTheOutcomeIsNotApproved_IsNotPaid()
    {
        // Arrange
        using var provider = new StubProvider(HttpStatusCode.Created, """{ "charge_id": "ch_1001", "outcome": "pending" }""");

        // Act
        bool paid = await Charge(provider);

        // Assert
        Assert.That(paid, Is.False);
    }

    [Test]
    public async Task Charge_WhenTheCallerCancels_IsCancelled()
    {
        // Arrange
        using var provider = new StubProvider(HttpStatusCode.Created, """{ "outcome": "approved" }""");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act
        Exception? thrown = null;
        try
        {
            await Charge(provider, cancelled.Token);
        }
        catch (OperationCanceledException exception)
        {
            thrown = exception;
        }

        // Assert
        Assert.That(thrown, Is.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task Charge_PostsTheFeeInTheProvidersTermsToItsChargesApi()
    {
        // Arrange
        using var provider = new StubProvider(HttpStatusCode.Created, """{ "charge_id": "ch_1001", "outcome": "approved" }""");

        // Act
        await Charge(provider);

        // Assert
        Assert.That(provider.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(provider.Path, Is.EqualTo("/v1/charges"));
        using var body = JsonDocument.Parse(provider.Body);
        Assert.That(body.RootElement.GetProperty("customer_reference").GetString(), Is.EqualTo(MemberId.ToString()));
        Assert.That(body.RootElement.GetProperty("amount_minor").GetInt64(), Is.EqualTo(29900));
        Assert.That(body.RootElement.GetProperty("currency").GetString(), Is.EqualTo("DKK"));
        Assert.That(body.RootElement.GetProperty("description").GetString(), Is.EqualTo("FitLife membership fee"));
    }

    private static async Task<bool> Charge(StubProvider provider, CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient(provider, disposeHandler: false) { BaseAddress = new Uri("https://payment-provider.test") };
        return await new PaymentProviderClient(http).ChargeMembershipFeeAsync(MemberId, 299m, cancellationToken).ConfigureAwait(false);
    }

    private sealed class StubProvider : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body = string.Empty;
        private readonly Exception? _failure;

        public StubProvider(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public StubProvider(Exception failure)
        {
            _failure = failure;
        }

        public HttpMethod? Method { get; private set; }

        public string? Path { get; private set; }

        public string Body { get; private set; } = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            if (_failure is not null)
            {
                throw _failure;
            }

            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }
}
