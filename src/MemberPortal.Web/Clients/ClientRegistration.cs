using FitLife.Correlation;

namespace MemberPortal.Web.Clients;

/// <summary>Registers the clients the portal calls the services with.</summary>
public static class ClientRegistration
{
    /// <summary>
    /// Adds <see cref="IdentityClient"/> as the <see cref="IIdentityClient"/>: a typed client with the gateway's address from
    /// <see cref="IdentityClient.BaseUrlConfigurationKey"/> and the correlation ID handler. Logging in needs no token, so it
    /// has no <see cref="BearerTokenHandler"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddIdentityClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<CorrelationIdHandler>();
        services.AddHttpClient<IIdentityClient, IdentityClient>((provider, http) =>
            {
                // Read when the client is made, so the portal starts without it (TEAM010: no address literal)
                string baseUrl = provider.GetRequiredService<IConfiguration>()[IdentityClient.BaseUrlConfigurationKey]
                    ?? throw new InvalidOperationException(
                        $"Found no {IdentityClient.BaseUrlConfigurationKey} in configuration. Set it to the gateway's address (deploy/compose/services.yml).");
                http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            })
            .AddHttpMessageHandler<CorrelationIdHandler>();

        return services;
    }
}
