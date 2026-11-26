using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FitLife.Authentication;

/// <summary>Lets a path with no endpoint answer 404 under the fallback policy of AddFitLifeAuthorization() (#440).</summary>
public static class NotFoundFallbackExtensions
{
    /// <summary>
    /// Maps an open fallback that answers 404. The fallback policy of
    /// <see cref="AuthorizationExtensions.AddFitLifeAuthorization"/> also covers a request that matches no endpoint,
    /// so without this an unknown path, or <c>/openapi/v1.json</c> outside Development, answers 401.
    /// The pattern is <c>{*path}</c>, not the default <c>{*path:nonfile}</c>, so a path that looks like a file
    /// answers 404 too. Call it after the service's own endpoints.
    /// </summary>
    /// <param name="endpoints">The app's endpoints.</param>
    /// <returns>The fallback endpoint's builder.</returns>
    public static IEndpointConventionBuilder MapFitLifeNotFoundFallback(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapFallback("{*path}", NotFound).AllowAnonymous();
    }

    private static IResult NotFound()
    {
        return Results.NotFound();
    }
}
