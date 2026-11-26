using FitLife.Correlation;
using MembershipService.Domain;

namespace MembershipService.PaymentProvider;

/// <summary>Registers the anticorruption layer to the payment provider.</summary>
public static class PaymentProviderRegistration
{
    /// <summary>
    /// Adds <see cref="PaymentProviderClient"/> as the <see cref="IPaymentProvider"/>, a typed client with the address
    /// from <see cref="PaymentProviderClient.BaseUrlConfigurationKey"/> and the correlation ID handler.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPaymentProvider(this IServiceCollection services)
    {
        services.AddTransient<CorrelationIdHandler>();
        services.AddHttpClient<IPaymentProvider, PaymentProviderClient>((provider, client) =>
            {
                string baseUrl = provider.GetRequiredService<IConfiguration>()[PaymentProviderClient.BaseUrlConfigurationKey]
                    ?? throw new InvalidOperationException(
                        $"Found no {PaymentProviderClient.BaseUrlConfigurationKey} in configuration. Set it to the payment provider's mock server (tests/postman/README.md).");
                client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<CorrelationIdHandler>();
        return services;
    }
}
