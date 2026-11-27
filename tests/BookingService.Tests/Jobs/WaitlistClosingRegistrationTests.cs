using BookingService.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BookingService.Tests.Jobs;

/// <summary>The waitlist-closing job runs only where WaitlistClosing:Enabled says so (compose, #314).</summary>
public sealed class WaitlistClosingRegistrationTests
{
    [TestCase("true", 1)]
    [TestCase("false", 0)]
    [TestCase(null, 0)]
    public void AddWaitlistClosing_RegistersTheJobOnlyWhenEnabled(string? enabled, int jobs)
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["WaitlistClosing:Enabled"] = enabled })
            .Build();
        var services = new ServiceCollection();

        // Act
        services.AddWaitlistClosing(configuration);

        // Assert
        Assert.That(services.Count(IsTheJob), Is.EqualTo(jobs));
    }

    private static bool IsTheJob(ServiceDescriptor descriptor)
    {
        return descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(WaitlistClosingJob);
    }
}
