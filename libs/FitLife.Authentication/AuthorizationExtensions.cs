using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FitLife.Authentication;

/// <summary>Sets up the role check, the same way in every service (#362).</summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers the policies in <see cref="FitLifePolicies"/>, and a fallback policy that requires a valid token,
    /// so an endpoint is closed until it is opened on purpose. Call it with <c>AddFitLifeJwtAuthentication()</c>.
    /// An endpoint without authorization metadata answers 401 without a token; one with a policy answers 403 to a
    /// token without its role. When a service adopts this, mark these <c>AllowAnonymous()</c>: <c>/health</c>,
    /// and in Development the OpenAPI document (<c>MapOpenApi()</c>) and Scalar (<c>MapScalarApiReference()</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddFitLifeAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(FitLifePolicies.Medlem, policy => policy.RequireRole(FitLifeRoles.Medlem))
            .AddPolicy(FitLifePolicies.TraenerOrAdmin, policy => policy.RequireRole(FitLifeRoles.Traener, FitLifeRoles.Admin))
            .AddPolicy(FitLifePolicies.Admin, policy => policy.RequireRole(FitLifeRoles.Admin));
        return services;
    }
}
