using System.Net;
using System.Text.RegularExpressions;
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
public sealed partial class LogInTests : IDisposable
{
    private const string AuthenticationCookie = ".AspNetCore.Cookies";
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
        Assert.That(new Uri(_client.BaseAddress!, response.Headers.Location!).AbsolutePath, Is.EqualTo("/"));
        Assert.That(SetCookie(response, AuthenticationCookie), Is.Not.Null);
    }

    [Test]
    public async Task LogIn_WithCorrectPassword_SetsASecureHttpOnlySameSiteLaxCookie()
    {
        // Act
        using HttpResponseMessage response = await LogInAsync().ConfigureAwait(false);

        // Assert
        string cookie = SetCookie(response, AuthenticationCookie)!.ToUpperInvariant();
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
        Assert.That(SetCookie(response, AuthenticationCookie), Is.Null);
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
        using var form = new FormUrlEncodedContent(HiddenFields(page));
        using HttpResponseMessage response = await _client.PostAsync(new Uri("/log-ud", UriKind.Relative), form).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(new Uri(_client.BaseAddress!, response.Headers.Location!).AbsolutePath, Is.EqualTo("/"));
        Assert.That(SetCookie(response, AuthenticationCookie), Does.Contain("expires=Thu, 01 Jan 1970"));
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
        Assert.That(SetCookie(response, AuthenticationCookie), Is.Null);
    }

    private async Task<HttpResponseMessage> LogInAsync()
    {
        string page = await _client.GetStringAsync(new Uri("/log-ind", UriKind.Relative)).ConfigureAwait(false);
        Dictionary<string, string> fields = HiddenFields(page);
        fields["Form.Email"] = "medlem@fitlife.test";
        fields["Form.Password"] = "hemmelig";
        using var form = new FormUrlEncodedContent(fields);
        return await _client.PostAsync(new Uri("/log-ind", UriKind.Relative), form).ConfigureAwait(false);
    }

    private static Dictionary<string, string> HiddenFields(string page)
    {
        return HiddenField().Matches(page).ToDictionary(match => WebUtility.HtmlDecode(match.Groups["name"].Value), match => WebUtility.HtmlDecode(match.Groups["value"].Value), StringComparer.Ordinal);
    }

    private static string? SetCookie(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies)
            ? cookies.FirstOrDefault(cookie => cookie.StartsWith(name + "=", StringComparison.Ordinal))
            : null;
    }

    [GeneratedRegex("""<input type="hidden" name="(?<name>[^"]+)" value="(?<value>[^"]*)" */?>""")]
    private static partial Regex HiddenField();

    private sealed class FakeIdentity : IIdentityClient
    {
        public const string AccessToken = "header.payload.signature-for-the-test";

        public TokenResponse? Token { get; set; } = new(AccessToken, ExpiresAt);

        public Task<TokenResponse?> LogInAsync(LogInRequest request, CancellationToken cancellationToken) => Task.FromResult(Token);
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
