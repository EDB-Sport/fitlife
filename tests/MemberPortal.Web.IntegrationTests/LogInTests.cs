using System.Net;
using MemberPortal.Web.Clients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace MemberPortal.Web.IntegrationTests;

/// <summary>
/// Logging in at <c>/log-ind</c> and out at <c>/log-ud</c> (#206), through the real pipeline with a fake Identity.
/// Each form post first gets the page and sends back its hidden fields, as a browser does.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // a new portal and cookie jar per test, so no test sees another's log-in
public sealed class LogInTests : IDisposable
{
    private static readonly DateTimeOffset ExpiresAt = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PortalFactory _factory = new();
    private readonly HttpClient _client;

    public LogInTests()
    {
        // HTTPS, because the cookie is Secure; no auto-redirect, so the tests see the redirects
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task LogIn_WithCorrectPassword_SetsTheCookieAndRedirectsToTheFrontPage()
    {
        // Act
        using HttpResponseMessage response = await LogInAsync().ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(PortalForms.RedirectPath(_client, response), Is.EqualTo("/"));
        Assert.That(PortalForms.SetCookie(response, PortalForms.AuthenticationCookie), Is.Not.Null);
    }

    [Test]
    public async Task LogIn_WithCorrectPassword_SetsASecureHttpOnlySameSiteLaxCookie()
    {
        // Act
        using HttpResponseMessage response = await LogInAsync().ConfigureAwait(false);

        // Assert
        string cookie = (PortalForms.SetCookie(response, PortalForms.AuthenticationCookie) ?? string.Empty).ToUpperInvariant();
        Assert.That(cookie, Does.Contain("; SECURE"));
        Assert.That(cookie, Does.Contain("; HTTPONLY"));
        Assert.That(cookie, Does.Contain("; SAMESITE=LAX"));
    }

    [Test]
    public async Task LogIn_WithCorrectPassword_ShowsTheEmailOnTheNextPage()
    {
        // Arrange
        using HttpResponseMessage logIn = await LogInAsync().ConfigureAwait(false);

        // Act
        string page = await _client.GetStringAsync(new Uri("/", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(page, Does.Contain("medlem@fitlife.test"));
        Assert.That(page, Does.Contain("Log ud"));
    }

    [Test]
    public async Task LogIn_WithCorrectPassword_NeverWritesTheTokenToThePage()
    {
        // Arrange
        using HttpResponseMessage logIn = await LogInAsync().ConfigureAwait(false);

        // Act
        string page = await _client.GetStringAsync(new Uri("/", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(page, Does.Not.Contain(FakeIdentity.AccessToken));
        Assert.That(logIn.Headers.Location?.ToString(), Does.Not.Contain(FakeIdentity.AccessToken));
    }

    [Test]
    public async Task LogIn_WithWrongPassword_SetsNoCookie()
    {
        // Arrange
        _factory.Identity.Token = null;

        // Act
        using HttpResponseMessage response = await LogInAsync().ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(PortalForms.SetCookie(response, PortalForms.AuthenticationCookie), Is.Null);
        string page = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.That(page, Does.Contain("Forkert e-mail eller adgangskode."));
    }

    [Test]
    public async Task LogOut_WhenLoggedIn_ClearsTheCookieAndRedirectsToTheFrontPage()
    {
        // Arrange
        using HttpResponseMessage logIn = await LogInAsync().ConfigureAwait(false);
        string page = await _client.GetStringAsync(new Uri("/", UriKind.Relative)).ConfigureAwait(false);

        // Act
        using var form = new FormUrlEncodedContent(PortalForms.HiddenFields(page));
        using HttpResponseMessage response = await _client.PostAsync(new Uri("/log-ud", UriKind.Relative), form).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(PortalForms.RedirectPath(_client, response), Is.EqualTo("/"));
        Assert.That(PortalForms.SetCookie(response, PortalForms.AuthenticationCookie), Does.Contain("expires=Thu, 01 Jan 1970"));
    }

    [Test]
    public async Task LogOut_WithoutTheAntiforgeryToken_IsRefused()
    {
        // Arrange
        using HttpResponseMessage logIn = await LogInAsync().ConfigureAwait(false);

        // Act
        using var form = new FormUrlEncodedContent([]);
        using HttpResponseMessage response = await _client.PostAsync(new Uri("/log-ud", UriKind.Relative), form).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(PortalForms.SetCookie(response, PortalForms.AuthenticationCookie), Is.Null);
    }

    private async Task<HttpResponseMessage> LogInAsync()
    {
        string page = await _client.GetStringAsync(new Uri("/log-ind", UriKind.Relative)).ConfigureAwait(false);
        Dictionary<string, string> fields = PortalForms.HiddenFields(page);
        fields["Form.Email"] = "medlem@fitlife.test";
        fields["Form.Password"] = "hemmelig";
        using var form = new FormUrlEncodedContent(fields);
        return await _client.PostAsync(new Uri("/log-ind", UriKind.Relative), form).ConfigureAwait(false);
    }

    private sealed class FakeIdentity : IIdentityClient
    {
        public const string AccessToken = "header.payload.signature-for-the-test";

        public TokenResponse? Token { get; set; } = new(AccessToken, ExpiresAt);

        public Task<CreateAccountAnswer> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Logging in creates no account.");
        }

        public Task<TokenResponse?> LogInAsync(LogInRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Token);
        }
    }

    private sealed class PortalFactory : WebApplicationFactory<Program>
    {
        public FakeIdentity Identity { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services => services.AddSingleton<IIdentityClient>(Identity));
        }
    }
}
