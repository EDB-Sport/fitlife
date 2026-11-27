using System.Net.Http.Headers;
using MemberPortal.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MemberPortal.Web.Clients;

/// <summary>
/// Adds <c>Authorization: Bearer &lt;token&gt;</c> to a call to a service when the request the portal serves carries the
/// authentication cookie, and no header when it doesn't (ADR 003). Booking's client gets it with #207, its first call that needs a log-in.
/// </summary>
/// <param name="httpContextAccessor">The request being served. Read when a call is sent, because <c>IHttpClientFactory</c> keeps a handler across requests.</param>
public sealed class BearerTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        HttpContext? context = httpContextAccessor.HttpContext;
        if (context is not null)
        {
            string? accessToken = await context.GetTokenAsync(CookieAuthenticationDefaults.AuthenticationScheme, PortalAuthentication.AccessTokenName).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
