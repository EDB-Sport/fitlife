using BookingService.Application;
using FitLife.Correlation;
using Microsoft.Extensions.Http.Resilience;

namespace BookingService.Clients;

/// <summary>Registers the client Booking asks Membership with.</summary>
public static class MembershipClientRegistration
{
    /// <summary>
    /// Adds <see cref="MembershipClient"/> as the <see cref="IMembershipClient"/>: a typed client with the address from
    /// <see cref="MembershipClient.BaseUrlConfigurationKey"/>, the correlation ID handler and the standard resilience handler
    /// (time-out, retry, circuit breaker) with its defaults.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMembershipClient(this IServiceCollection services)
    {
        return services.AddMembershipClient(configureResilience: null);
    }

    /// <summary>As <see cref="AddMembershipClient(IServiceCollection)"/>; the tests shorten the resilience delays here.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureResilience">Changes the standard handler's options, or null for its defaults.</param>
    /// <returns>The service collection.</returns>
    internal static IServiceCollection AddMembershipClient(this IServiceCollection services, Action<HttpStandardResilienceOptions>? configureResilience)
    {
        services.AddTransient<CorrelationIdHandler>();
        IHttpClientBuilder client = services.AddHttpClient<IMembershipClient, MembershipClient>((provider, http) =>
            {
                // Read when the client is made, so the service starts without it (TEAM010: no address literal)
                string baseUrl = provider.GetRequiredService<IConfiguration>()[MembershipClient.BaseUrlConfigurationKey]
                    ?? throw new InvalidOperationException(
                        $"Found no {MembershipClient.BaseUrlConfigurationKey} in configuration. Set it to Membership's address (deploy/compose/services.yml).");
                http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            })
            .AddHttpMessageHandler<CorrelationIdHandler>();
        if (configureResilience is null)
        {
            client.AddStandardResilienceHandler();
        }
        else
        {
            client.AddStandardResilienceHandler(configureResilience);
        }

        return services;
    }
}
