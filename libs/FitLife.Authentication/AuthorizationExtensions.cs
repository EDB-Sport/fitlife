using Microsoft.Extensions.DependencyInjection;

namespace FitLife.Authentication;

/// <summary>Sets up the role check, the same way in every service (#362).</summary>
public static class AuthorizationExtensions
{
    /// <summary>Not built yet (#362).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddFitLifeAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();
        return services;
    }
}
