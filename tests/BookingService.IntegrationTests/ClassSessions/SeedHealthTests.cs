using System.Net;
using BookingService.Application;
using BookingService.Models;
using BookingService.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>
/// The seed runs in the background, and /health is unhealthy until it has finished (#255), so <c>docker compose up --wait</c>
/// and api-tests wait for the demo data.
/// </summary>
public sealed class SeedHealthTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Health_UntilTheSeedHasFinished_IsUnhealthy()
    {
        // Arrange: the seed waits at its first write until the test lets it go
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var booking = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider(Now), seed: true);
        using WebApplicationFactory<Program> factory = booking.WithWebHostBuilder(host => UseGatedWriter(host, gate.Task));
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage health = await client.GetAsync(new Uri("/health", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        gate.SetResult();
        Assert.That(health.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task Health_AfterTheSeed_IsHealthy()
    {
        // Arrange
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider(Now), seed: true);
        using HttpClient client = factory.CreateClient();
        await factory.SeedFinishedAsync().ConfigureAwait(false);

        // Act
        using HttpResponseMessage health = await client.GetAsync(new Uri("/health", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(health.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static void UseGatedWriter(IWebHostBuilder host, Task gate)
    {
        host.ConfigureTestServices(services => ReplaceWriter(services, gate));
    }

    private static void ReplaceWriter(IServiceCollection services, Task gate)
    {
        services.Replace(ServiceDescriptor.Scoped<IReferenceDataWriter>(provider =>
            new GatedReferenceDataWriter(ActivatorUtilities.CreateInstance<ReferenceDataWriter>(provider), gate)));
    }

    /// <summary>Writes reference data only once the gate opens, so the seed is still running when the test asks.</summary>
    private sealed class GatedReferenceDataWriter(IReferenceDataWriter inner, Task gate) : IReferenceDataWriter
    {
        public async Task SaveCenterAsync(Center center, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            await inner.SaveCenterAsync(center, cancellationToken).ConfigureAwait(false);
        }

        public async Task SaveClassTypeAsync(ClassType classType, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            await inner.SaveClassTypeAsync(classType, cancellationToken).ConfigureAwait(false);
        }
    }
}
