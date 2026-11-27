using System.Security.Claims;
using MemberPortal.Web.Authentication;
using MemberPortal.Web.Clients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPortal.Web.Tests;

/// <summary>
/// <see cref="PortalAuthentication.SignInAsync"/> signs the person in with the cookie (#206): the ticket ends when the
/// token does, and the token is in the ticket's properties.
/// </summary>
public sealed class PortalAuthenticationTests
{
    private static readonly DateTimeOffset ExpiresAt = new(2026, 11, 26, 15, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task SignInAsync_SignsInWithTheCookieTheEmailTheTokenAndItsExpiry()
    {
        // Arrange
        var authentication = new RecordingAuthentication();
        ServiceProvider services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };

        // Act
        await PortalAuthentication.SignInAsync(context, "medlem@fitlife.test", new TokenResponse("header.payload.signature", ExpiresAt)).ConfigureAwait(false);

        // Assert
        Assert.That(authentication.Scheme, Is.EqualTo(CookieAuthenticationDefaults.AuthenticationScheme));
        Assert.That(authentication.Principal?.Identity?.Name, Is.EqualTo("medlem@fitlife.test"));
        Assert.That(authentication.Properties?.ExpiresUtc, Is.EqualTo(ExpiresAt));
        Assert.That(authentication.Properties?.GetTokenValue(PortalAuthentication.AccessTokenName), Is.EqualTo("header.payload.signature"));
    }

    /// <summary>The authentication service: remembers the sign-in.</summary>
    private sealed class RecordingAuthentication : IAuthenticationService
    {
        public string? Scheme { get; private set; }

        public ClaimsPrincipal? Principal { get; private set; }

        public AuthenticationProperties? Properties { get; private set; }

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            Scheme = scheme;
            Principal = principal;
            Properties = properties;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            throw new NotSupportedException();
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }
    }
}
