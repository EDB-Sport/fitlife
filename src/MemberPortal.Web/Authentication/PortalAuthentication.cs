using System.Security.Claims;
using MemberPortal.Web.Clients;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MemberPortal.Web.Authentication;

/// <summary>
/// Logging in and out of the portal (#206, ADR 003). The token from Identity is kept in the authentication cookie's
/// properties, so the browser holds only an encrypted, HTTP-only cookie and never the token itself.
/// </summary>
public static class PortalAuthentication
{
    /// <summary>The name the access token is stored under in the cookie's authentication properties.</summary>
    public const string AccessTokenName = "access_token";

    /// <summary>The log-in page.</summary>
    public const string LogInPath = "/log-ind";

    /// <summary>Where the "Log ud" form posts.</summary>
    public const string LogOutPath = "/log-ud";

    /// <summary>Where the portal goes after logging in and after logging out: the front page.</summary>
    public const string FrontPagePath = "/";

    /// <summary>
    /// Adds cookie authentication with <see cref="LogInPath"/> as the log-in path, authorization, the authentication state
    /// for the components, and the <see cref="BearerTokenHandler"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPortalAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = LogInPath;
                // Set explicitly (ADR 003): the default SecurePolicy, SameAsRequest, would send the cookie over plain HTTP.
                // Lax, not Strict: a person who follows a link to the portal from another site is still logged in
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                // The ticket ends when the token does (ExpiresUtc at sign-in), and sliding mustn't move it past that
                options.SlidingExpiration = false;
            });
        services.AddAuthorization();
        services.AddCascadingAuthenticationState();
        services.AddHttpContextAccessor();
        services.AddTransient<BearerTokenHandler>();
        return services;
    }

    /// <summary>
    /// Signs the person in with the cookie: their e-mail address as the name, the token in the cookie's properties, and
    /// the token's expiry as the ticket's.
    /// </summary>
    /// <param name="context">The request that logs in.</param>
    /// <param name="email">The e-mail address the person logged in with.</param>
    /// <param name="token">Identity's token.</param>
    /// <returns>A task that completes when the cookie is set on the response.</returns>
    public static Task SignInAsync(HttpContext context, string email, TokenResponse token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, email)], CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties { ExpiresUtc = token.ExpiresAt };
        properties.StoreTokens([new AuthenticationToken { Name = AccessTokenName, Value = token.AccessToken }]);
        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
    }

    /// <summary>Maps <c>POST</c> <see cref="LogOutPath"/>: needs the antiforgery token, removes the cookie and redirects to the front page.</summary>
    /// <param name="endpoints">The app's endpoints.</param>
    /// <returns>The endpoint's builder.</returns>
    public static IEndpointConventionBuilder MapLogOut(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost(LogOutPath, (Func<HttpContext, Task<IResult>>)LogOutAsync).WithMetadata(new RequireAntiforgeryTokenAttribute());
    }

    private static async Task<IResult> LogOutAsync(HttpContext context)
    {
        // UseAntiforgery only records the result for an endpoint that binds no form, so the endpoint refuses a bad token itself
        if (context.Features.Get<IAntiforgeryValidationFeature>() is not { IsValid: true })
        {
            return Results.BadRequest();
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        return Results.LocalRedirect(FrontPagePath);
    }
}
