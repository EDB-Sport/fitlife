using System.Net;
using System.Security.Claims;
using MemberPortal.Web.Clients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPortal.Web.Tests;

/// <summary>
/// <see cref="BearerTokenHandler"/> sends the logged-in person's token to the services (#206). The token comes from the
/// authentication cookie of the request that is being served.
/// </summary>
public sealed class BearerTokenHandlerTests
{
    [Test]
    public async Task BearerTokenHandler_WhenLoggedIn_AddsTheBearerHeader()
    {
        // Arrange
        HttpContext context = Context(new FakeAuthentication("header.payload.signature"));

        // Act
        HttpRequestMessage sent = await Send(context).ConfigureAwait(false);

        // Assert
        Assert.That(sent.Headers.Authorization?.ToString(), Is.EqualTo("Bearer header.payload.signature"));
    }

    [Test]
    public async Task BearerTokenHandler_WhenNotLoggedIn_AddsNoHeader()
    {
        // Arrange
        HttpContext context = Context(new FakeAuthentication(accessToken: null));

        // Act
        HttpRequestMessage sent = await Send(context).ConfigureAwait(false);

        // Assert
        Assert.That(sent.Headers.Authorization, Is.Null);
    }

    [Test]
    public async Task BearerTokenHandler_OutsideARequest_AddsNoHeader()
    {
        // Arrange & Act
        HttpRequestMessage sent = await Send(context: null).ConfigureAwait(false);

        // Assert
        Assert.That(sent.Headers.Authorization, Is.Null);
    }

    private static HttpContext Context(FakeAuthentication authentication)
    {
        ServiceProvider services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private static async Task<HttpRequestMessage> Send(HttpContext? context)
    {
        using var service = new RecordingService();
        using var handler = new BearerTokenHandler(new HttpContextAccessor { HttpContext = context }) { InnerHandler = service };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://gateway.test/api/booking/class-sessions"));

        using HttpResponseMessage response = await invoker.SendAsync(request, CancellationToken.None).ConfigureAwait(false);

        return service.Request!;
    }

    /// <summary>The cookie scheme: a logged-in person's ticket with the token in its properties, or nobody.</summary>
    private sealed class FakeAuthentication(string? accessToken) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            if (accessToken is null || scheme != CookieAuthenticationDefaults.AuthenticationScheme)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = accessToken }]);
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "medlem@fitlife.test")], scheme));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, properties, scheme)));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => throw new NotSupportedException();

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
    }

    /// <summary>The service behind the gateway: remembers the request and answers 200.</summary>
    private sealed class RecordingService : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
