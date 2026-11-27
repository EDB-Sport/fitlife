using System.Net;
using System.Text;
using MemberPortal.Web.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPortal.Web.Tests;

/// <summary>
/// The portal joins with <c>POST api/membership/members</c> through the gateway (#209). The real registration, with a
/// stub handler in place of the gateway.
/// </summary>
public sealed class MembershipClientTests
{
    private const string Member = """{"memberId":"0b6f5f7e-2f0c-4b8e-9a51-6d2f4c1e7a90","membershipStatus":"PaymentMissing","isActive":false}""";

    [Test]
    public async Task JoinAsync_PostsToMembershipThroughTheGatewayWithTheGivenToken()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.Created, Member);
        using ServiceProvider services = Services(gateway);

        // Act
        await Join(services, "fresh.token.signature").ConfigureAwait(false);

        // Assert
        Assert.That(gateway.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(gateway.Uri, Is.EqualTo(new Uri("https://gateway.test/api/membership/members")));
        Assert.That(gateway.Authorization, Is.EqualTo("Bearer fresh.token.signature"));
    }

    [Test]
    public async Task JoinAsync_WithoutAToken_LeavesTheHeaderToTheBearerHandler()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.Created, Member);
        using ServiceProvider services = Services(gateway);

        // Act
        await Join(services, accessToken: null).ConfigureAwait(false);

        // Assert: outside a request the bearer handler finds no cookie, so no header at all
        Assert.That(gateway.Authorization, Is.Null);
    }

    [TestCase(HttpStatusCode.Created, JoinMembershipAnswer.Joined)]
    [TestCase(HttpStatusCode.Conflict, JoinMembershipAnswer.AlreadyAMember)]
    public async Task JoinAsync_ReturnsMembershipsAnswer(HttpStatusCode status, JoinMembershipAnswer expected)
    {
        // Arrange
        using var gateway = new StubGateway(status, status == HttpStatusCode.Created ? Member : """{"status":409}""");
        using ServiceProvider services = Services(gateway);

        // Act
        JoinMembershipAnswer answer = await Join(services, "fresh.token.signature").ConfigureAwait(false);

        // Assert
        Assert.That(answer, Is.EqualTo(expected));
    }

    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public async Task JoinAsync_OnAnotherError_ThrowsMembershipUnavailable(HttpStatusCode status)
    {
        // Arrange
        using var gateway = new StubGateway(status, "{}");
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => Join(services, "fresh.token.signature"), Throws.TypeOf<MembershipUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task JoinAsync_WhenTheGatewayCantBeReached_ThrowsMembershipUnavailable()
    {
        // Arrange
        using var gateway = new StubGateway(new HttpRequestException("No connection could be made."));
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => Join(services, "fresh.token.signature"), Throws.TypeOf<MembershipUnavailableException>()).ConfigureAwait(false);
    }

    private static Task<JoinMembershipAnswer> Join(ServiceProvider services, string? accessToken)
    {
        return services.GetRequiredService<IMembershipClient>().JoinAsync(accessToken, CancellationToken.None);
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
