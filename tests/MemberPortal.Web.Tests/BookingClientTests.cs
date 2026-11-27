using System.Net;
using System.Text;
using System.Text.Json;
using FitLife.Correlation;
using MemberPortal.Web.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MemberPortal.Web.Tests;

/// <summary>
/// The portal reads the class schedule with <c>GET api/booking/class-sessions</c> through the gateway (#205). The real
/// registration, with a stub handler in place of the gateway.
/// </summary>
public sealed class BookingClientTests
{
    private const string Schedule = """
        [{"id":"0b6f5f7e-2f0c-4b8e-9a51-6d2f4c1e7a90","centerId":"6a0c3f4e-1b2d-4c5e-8f90-1a2b3c4d5e01","centerName":"FitLife Aarhus",
          "classTypeId":"7d1e2f3a-4b5c-4d6e-8f70-8a9b0c1d2e03","classTypeName":"Spinning","instructorName":"Louise",
          "startTime":"2026-12-01T07:00:00+01:00","durationMinutes":45,"capacity":20,"freeSeats":0,"isCancelled":true}]
        """;

    [Test]
    public async Task BookingClient_GetClassSessions_ReadsTheListFromTheGatewayPath()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, Schedule);
        using ServiceProvider services = Services(gateway);

        // Act
        IReadOnlyList<ClassSessionResponse> sessions = await GetClassSessions(services).ConfigureAwait(false);

        // Assert
        Assert.That(gateway.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(gateway.Uri, Is.EqualTo(new Uri("https://gateway.test/api/booking/class-sessions")));
        Assert.That(sessions, Is.EqualTo(new[]
        {
            new ClassSessionResponse(
                new Guid("0b6f5f7e-2f0c-4b8e-9a51-6d2f4c1e7a90"),
                new Guid("6a0c3f4e-1b2d-4c5e-8f90-1a2b3c4d5e01"),
                "FitLife Aarhus",
                new Guid("7d1e2f3a-4b5c-4d6e-8f70-8a9b0c1d2e03"),
                "Spinning",
                "Louise",
                new DateTimeOffset(2026, 12, 1, 7, 0, 0, TimeSpan.FromHours(1)),
                45,
                20,
                0,
                IsCancelled: true),
        }));
    }

    [Test]
    public async Task BookingClient_GetClassSessions_SendsTheCorrelationId()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, "[]");
        using ServiceProvider services = Services(gateway);
        using IDisposable scope = CorrelationContext.Begin(NullLogger.Instance, "c-205");

        // Act
        await GetClassSessions(services).ConfigureAwait(false);

        // Assert
        Assert.That(gateway.CorrelationId, Is.EqualTo("c-205"));
    }

    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.NotFound)]
    public async Task BookingClient_GetClassSessions_OnAnError_ThrowsBookingUnavailable(HttpStatusCode status)
    {
        // Arrange
        using var gateway = new StubGateway(status, "{}");
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => GetClassSessions(services), Throws.TypeOf<BookingUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task BookingClient_GetClassSessions_WhenTheGatewayCantBeReached_ThrowsBookingUnavailable()
    {
        // Arrange
        using var gateway = new StubGateway(new HttpRequestException("No connection could be made."));
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => GetClassSessions(services), Throws.TypeOf<BookingUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task BookingClient_GetClassSessions_WhenBookingDoesNotAnswerInTime_ThrowsBookingUnavailable()
    {
        // Arrange: HttpClient's timeout arrives as a TaskCanceledException
        using var gateway = new StubGateway(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => GetClassSessions(services), Throws.TypeOf<BookingUnavailableException>()).ConfigureAwait(false);
    }

    [TestCase("not json")]
    [TestCase("null")]
    public async Task BookingClient_GetClassSessions_WhenTheBodyIsNotAList_ThrowsBookingUnavailable(string body)
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, body);
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => GetClassSessions(services), Throws.TypeOf<BookingUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public void ClassSessionResponse_HasTheFieldsOfBookingsContract()
    {
        // Arrange
        string[] contract = ContractProperties("ClassSessionResponse");

        // Act
        string[] fields = [.. typeof(ClassSessionResponse).GetProperties().Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name)).Order(StringComparer.Ordinal)];

        // Assert
        Assert.That(fields, Is.EqualTo(contract));
    }

    private static Task<IReadOnlyList<ClassSessionResponse>> GetClassSessions(ServiceProvider services)
    {
        return services.GetRequiredService<IBookingClient>().GetClassSessionsAsync(CancellationToken.None);
    }

    private static ServiceProvider Services(StubGateway gateway)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [IdentityClient.BaseUrlConfigurationKey] = "https://gateway.test" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddBookingClient();
        services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => gateway));
        return services.BuildServiceProvider();
    }

    // The property names of one schema in the committed contracts/openapi/bookingservice.json, sorted
    private static string[] ContractProperties(string schema)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "contracts", "openapi", "bookingservice.json")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new FileNotFoundException("Found no contracts/openapi/bookingservice.json above the test directory.");
        }

        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "contracts", "openapi", "bookingservice.json")));
        JsonElement properties = contract.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schema).GetProperty("properties");
        return [.. properties.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];
    }

    /// <summary>The gateway in front of Booking: answers with one status and body, and remembers the request.</summary>
    private sealed class StubGateway : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly Exception? _failure;

        public StubGateway(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public StubGateway(Exception failure)
        {
            _status = HttpStatusCode.OK;
            _body = string.Empty;
            _failure = failure;
        }

        public HttpMethod? Method { get; private set; }

        public Uri? Uri { get; private set; }

        public string? CorrelationId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
            if (request.Headers.TryGetValues(CorrelationContext.HeaderName, out IEnumerable<string>? values))
            {
                CorrelationId = values.Single();
            }

            if (_failure is not null)
            {
                throw _failure;
            }

            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
        }
    }
}
