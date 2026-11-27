using System.Net;
using System.Text;
using System.Text.Json;
using MemberPortal.Web.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPortal.Web.Tests;

/// <summary>
/// The portal logs in through the gateway with <c>POST api/identity/tokens</c> (#206), and creates an account with <c>POST api/identity/accounts</c> (#209). The real registration, with a stub
/// handler in place of the gateway.
/// </summary>
public sealed class IdentityClientTests
{
    private const string Token = """{"accessToken":"header.payload.signature","expiresAt":"2026-12-01T18:00:00+00:00"}""";
    private const string Account = """{"id":"7d0c6a4e-3a52-4f7e-9d43-1c2b8f0e5a11","email":"medlem@fitlife.test","roles":["Medlem"]}""";

    [Test]
    public async Task LogInAsync_PostsTheEmailAndPasswordToIdentityThroughTheGateway()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, Token);
        using ServiceProvider services = Services(gateway);

        // Act
        await LogIn(services).ConfigureAwait(false);

        // Assert
        Assert.That(gateway.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(gateway.Uri, Is.EqualTo(new Uri("https://gateway.test/api/identity/tokens")));
        Assert.That(gateway.Body, Is.EqualTo("""{"email":"medlem@fitlife.test","password":"hemmelig"}"""));
    }

    [Test]
    public async Task LogInAsync_On200_ReturnsTheTokenAndWhenItExpires()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, Token);
        using ServiceProvider services = Services(gateway);

        // Act
        TokenResponse? token = await LogIn(services).ConfigureAwait(false);

        // Assert
        Assert.That(token, Is.EqualTo(new TokenResponse("header.payload.signature", new DateTimeOffset(2026, 12, 1, 18, 0, 0, TimeSpan.Zero))));
    }

    [Test]
    public async Task LogInAsync_On401_ReturnsNoToken()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.Unauthorized, """{"status":401}""");
        using ServiceProvider services = Services(gateway);

        // Act
        TokenResponse? token = await LogIn(services).ConfigureAwait(false);

        // Assert
        Assert.That(token, Is.Null);
    }

    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    public async Task LogInAsync_OnAnotherError_ThrowsIdentityUnavailable(HttpStatusCode status)
    {
        // Arrange
        using var gateway = new StubGateway(status, "{}");
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => LogIn(services), Throws.TypeOf<IdentityUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task LogInAsync_WhenTheGatewayCantBeReached_ThrowsIdentityUnavailable()
    {
        // Arrange
        using var gateway = new StubGateway(new HttpRequestException("No connection could be made."));
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => LogIn(services), Throws.TypeOf<IdentityUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task LogInAsync_WhenTheAnswerCantBeRead_ThrowsIdentityUnavailable()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, "not json");
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => LogIn(services), Throws.TypeOf<IdentityUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task CreateAccountAsync_PostsTheEmailAndPasswordToIdentityThroughTheGateway()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.Created, Account);
        using ServiceProvider services = Services(gateway);

        // Act
        await CreateAccount(services).ConfigureAwait(false);

        // Assert
        Assert.That(gateway.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(gateway.Uri, Is.EqualTo(new Uri("https://gateway.test/api/identity/accounts")));
        Assert.That(gateway.Body, Is.EqualTo("""{"email":"medlem@fitlife.test","password":"hemmelig"}"""));
    }

    [TestCase(HttpStatusCode.Created, CreateAccountAnswer.Created)]
    [TestCase(HttpStatusCode.Conflict, CreateAccountAnswer.EmailAlreadyTaken)]
    [TestCase(HttpStatusCode.BadRequest, CreateAccountAnswer.Rejected)]
    public async Task CreateAccountAsync_ReturnsIdentitysAnswer(HttpStatusCode status, CreateAccountAnswer expected)
    {
        // Arrange
        using var gateway = new StubGateway(status, status == HttpStatusCode.Conflict ? """{"status":409,"code":"EmailAlreadyTaken"}""" : Account);
        using ServiceProvider services = Services(gateway);

        // Act
        CreateAccountAnswer answer = await CreateAccount(services).ConfigureAwait(false);

        // Assert
        Assert.That(answer, Is.EqualTo(expected));
    }

    [Test]
    public async Task CreateAccountAsync_On409WithAnotherCode_ThrowsIdentityUnavailable()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.Conflict, """{"status":409,"code":"SomethingElse"}""");
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => CreateAccount(services), Throws.TypeOf<IdentityUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task CreateAccountAsync_OnAnotherError_ThrowsIdentityUnavailable()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.InternalServerError, "{}");
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => CreateAccount(services), Throws.TypeOf<IdentityUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task CreateAccountAsync_WhenTheGatewayCantBeReached_ThrowsIdentityUnavailable()
    {
        // Arrange
        using var gateway = new StubGateway(new HttpRequestException("No connection could be made."));
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => CreateAccount(services), Throws.TypeOf<IdentityUnavailableException>()).ConfigureAwait(false);
    }

    [TestCase(typeof(LogInRequest), "LogInRequest")]
    [TestCase(typeof(TokenResponse), "TokenResponse")]
    [TestCase(typeof(CreateAccountRequest), "CreateAccountRequest")]
    public void Record_HasTheFieldsOfIdentitysContract(Type record, string schema)
    {
        // Arrange
        string[] contract = ContractProperties(schema);

        // Act
        string[] fields = [.. record.GetProperties().Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name)).Order(StringComparer.Ordinal)];

        // Assert
        Assert.That(fields, Is.EqualTo(contract));
    }

    private static Task<TokenResponse?> LogIn(ServiceProvider services)
    {
        return services.GetRequiredService<IIdentityClient>()
            .LogInAsync(new LogInRequest("medlem@fitlife.test", "hemmelig"), CancellationToken.None);
    }

    private static Task<CreateAccountAnswer> CreateAccount(ServiceProvider services)
    {
        return services.GetRequiredService<IIdentityClient>()
            .CreateAccountAsync(new CreateAccountRequest("medlem@fitlife.test", "hemmelig"), CancellationToken.None);
    }

    private static ServiceProvider Services(StubGateway gateway)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [IdentityClient.BaseUrlConfigurationKey] = "https://gateway.test" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddIdentityClient();
        services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => gateway));
        return services.BuildServiceProvider();
    }

    // The property names of one schema in the committed contracts/openapi/identityservice.json, sorted
    private static string[] ContractProperties(string schema)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "contracts", "openapi", "identityservice.json")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new FileNotFoundException("Found no contracts/openapi/identityservice.json above the test directory.");
        }

        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "contracts", "openapi", "identityservice.json")));
        JsonElement properties = contract.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schema).GetProperty("properties");
        return [.. properties.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];
    }

    /// <summary>The gateway in front of Identity: answers with one status and body, and remembers the request.</summary>
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

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
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
