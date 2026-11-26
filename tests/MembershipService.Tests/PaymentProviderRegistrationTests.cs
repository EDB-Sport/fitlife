using MembershipService.Domain;
using MembershipService.PaymentProvider;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MembershipService.Tests;

/// <summary>The payment provider's address comes from configuration, and the client is ready to use (#174).</summary>
public sealed class PaymentProviderRegistrationTests
{
    [Test]
    public void AddPaymentProvider_WithTheProvidersAddress_GivesTheClientWithThatAddress()
    {
        // Arrange
        using ServiceProvider services = Services("https://payment-provider.test");

        // Act
        IPaymentProvider payments = services.GetRequiredService<IPaymentProvider>();

        // Assert
        Assert.That(payments, Is.InstanceOf<PaymentProviderClient>());
    }

    [Test]
    public void AddPaymentProvider_WithoutTheProvidersAddress_FailsAndNamesTheConfigurationKey()
    {
        // Arrange
        using ServiceProvider services = Services(baseUrl: null);

        // Act
        Action act = () => services.GetRequiredService<IPaymentProvider>();

        // Assert
        Assert.That(act, Throws.InvalidOperationException.With.Message.Contains(PaymentProviderClient.BaseUrlConfigurationKey));
    }

    private static ServiceProvider Services(string? baseUrl)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [PaymentProviderClient.BaseUrlConfigurationKey] = baseUrl })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddPaymentProvider();
        return services.BuildServiceProvider();
    }
}
