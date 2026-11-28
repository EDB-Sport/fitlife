using System.Net;
using System.Text;
using System.Text.Json;
using MemberPortal.Web.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPortal.Web.Tests;

/// <summary>
/// The portal reads the member's own membership with <c>GET api/membership/members/me</c> through the gateway (#624).
/// The real registration, with a stub handler in place of the gateway. Joining is in <see cref="MembershipClientTests"/>.
/// </summary>
public sealed class MyMembershipClientTests
{
    private const string MyMembership = """{"memberId":"0b6f5f7e-2f0c-4b8e-9a51-6d2f4c1e7a90","membershipType":"Standard","membershipStatus":"Paused","isActive":false,"startDate":"2026-11-03"}""";

    [Test]
    public async Task GetMyMembershipAsync_GetsFromMembershipThroughTheGateway()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, MyMembership);
        using ServiceProvider services = Services(gateway);

        // Act
        await GetMyMembership(services).ConfigureAwait(false);

        // Assert
        Assert.That(gateway.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(gateway.Uri, Is.EqualTo(new Uri("https://gateway.test/api/membership/members/me")));
    }

    [Test]
    public async Task GetMyMembershipAsync_On200_ReadsTheMembership()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, MyMembership);
        using ServiceProvider services = Services(gateway);

        // Act
        MyMembershipResponse? membership = await GetMyMembership(services).ConfigureAwait(false);

        // Assert
        Assert.That(membership, Is.EqualTo(new MyMembershipResponse(
            new Guid("0b6f5f7e-2f0c-4b8e-9a51-6d2f4c1e7a90"), "Standard", "Paused", IsActive: false, new DateOnly(2026, 11, 3))));
    }

    [Test]
    public async Task GetMyMembershipAsync_On404_ReturnsNoMembership()
    {
        // Arrange: the account has not joined
        using var gateway = new StubGateway(HttpStatusCode.NotFound, string.Empty);
        using ServiceProvider services = Services(gateway);

        // Act
        MyMembershipResponse? membership = await GetMyMembership(services).ConfigureAwait(false);

        // Assert
        Assert.That(membership, Is.Null);
    }

    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public async Task GetMyMembershipAsync_OnAnotherError_ThrowsMembershipUnavailable(HttpStatusCode status)
    {
        // Arrange
        using var gateway = new StubGateway(status, "{}");
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => GetMyMembership(services), Throws.TypeOf<MembershipUnavailableException>()).ConfigureAwait(false);
    }

    [TestCase("not json")]
    [TestCase("null")]
    public async Task GetMyMembershipAsync_WhenTheBodyIsNotAMembership_ThrowsMembershipUnavailable(string body)
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, body);
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => GetMyMembership(services), Throws.TypeOf<MembershipUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task GetMyMembershipAsync_WhenTheGatewayCantBeReached_ThrowsMembershipUnavailable()
    {
        // Arrange
        using var gateway = new StubGateway(new HttpRequestException("No connection could be made."));
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => GetMyMembership(services), Throws.TypeOf<MembershipUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task GetMyMembershipAsync_WhenMembershipDoesNotAnswerInTime_ThrowsMembershipUnavailable()
    {
        // Arrange: HttpClient's timeout arrives as a TaskCanceledException
        using var gateway = new StubGateway(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => GetMyMembership(services), Throws.TypeOf<MembershipUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public void MyMembershipResponse_HasTheFieldsOfMembershipsContract()
    {
        // Arrange
        string[] contract = ContractProperties("MyMembershipResponse");

        // Act
        string[] fields = [.. typeof(MyMembershipResponse).GetProperties().Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name)).Order(StringComparer.Ordinal)];

        // Assert
        Assert.That(fields, Is.EqualTo(contract));
    }

    private static Task<MyMembershipResponse?> GetMyMembership(ServiceProvider services)
    {
        return services.GetRequiredService<IMembershipClient>().GetMyMembershipAsync(CancellationToken.None);
    }

    // The property names of one schema in the committed contracts/openapi/membershipservice.json, sorted
    private static string[] ContractProperties(string schema)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "contracts", "openapi", "membershipservice.json")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new FileNotFoundException("Found no contracts/openapi/membershipservice.json above the test directory.");
        }

        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "contracts", "openapi", "membershipservice.json")));
        JsonElement properties = contract.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schema).GetProperty("properties");
        return [.. properties.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];
    }

    private static ServiceProvider Services(StubGateway gateway)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [IdentityClient.BaseUrlConfigurationKey] = "https://gateway.test" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddHttpContextAccessor();
        services.AddMembershipClient();
        services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => gateway));
        return services.BuildServiceProvider();
    }

    /// <summary>The gateway in front of Membership: answers with one status and body, and remembers the request.</summary>
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

        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            if (_failure is not null)
            {
                throw _failure;
            }

            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
        }
    }
}
