using FitLife.Correlation;

namespace McpServer.Booking;

/// <summary>Registers the client the server asks Booking with.</summary>
public static class BookingClientRegistration
{
    /// <summary>
    /// Adds <see cref="BookingClient"/> as the <see cref="IBookingClient"/>: a typed client with the gateway's address from
    /// <see cref="BookingClient.BaseUrlConfigurationKey"/> and the correlation ID handler.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddBookingClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<CorrelationIdHandler>();
        services.AddHttpClient<IBookingClient, BookingClient>((provider, http) =>
            {
                // Read when the client is made, so the server starts without it and nothing connects at start-up (TEAM010: no address literal)
                string baseUrl = provider.GetRequiredService<IConfiguration>()[BookingClient.BaseUrlConfigurationKey]
                    ?? throw new InvalidOperationException(
                        $"Found no {BookingClient.BaseUrlConfigurationKey} in configuration. Set it to the gateway's address (deploy/compose/services.yml).");
                http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            })
            .AddHttpMessageHandler<CorrelationIdHandler>();

        return services;
    }
}
