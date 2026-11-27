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
        services.AddHttpClient<IIdentityClient, IdentityClient>((provider, http) => http.BaseAddress = GatewayAddress(provider))
            .AddHttpMessageHandler<CorrelationIdHandler>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="MembershipClient"/> as the <see cref="IMembershipClient"/>: a typed client with the gateway's address
    /// from <see cref="IdentityClient.BaseUrlConfigurationKey"/>, the correlation ID handler and the <see cref="BearerTokenHandler"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMembershipClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<CorrelationIdHandler>();
        services.AddTransient<BearerTokenHandler>();
        services.AddHttpClient<IMembershipClient, MembershipClient>((provider, http) => http.BaseAddress = GatewayAddress(provider))
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddHttpMessageHandler<BearerTokenHandler>();

        return services;
    }

    // Read when a client is made, so the portal starts without it (TEAM010: no address literal)
    private static Uri GatewayAddress(IServiceProvider provider)
    {
        string baseUrl = provider.GetRequiredService<IConfiguration>()[IdentityClient.BaseUrlConfigurationKey]
            ?? throw new InvalidOperationException(
                $"Found no {IdentityClient.BaseUrlConfigurationKey} in configuration. Set it to the gateway's address (deploy/compose/services.yml).");
        return new Uri(baseUrl, UriKind.Absolute);
    }
}
