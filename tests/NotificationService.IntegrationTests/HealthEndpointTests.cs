using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace NotificationService.IntegrationTests;

/// <summary>The /health endpoint that docker compose --wait and the Azure probes use: healthy only once events are consumed (#258).</summary>
public sealed class HealthEndpointTests
{
    [Test]
    public async Task GetHealth_OnceConsuming_ReturnsOkAndHealthy()
    {
        // Arrange
        using var factory = new NotificationServiceFactory();
        using HttpClient client = factory.CreateClient();
        await factory.WaitUntilConsumingAsync().ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync().ConfigureAwait(false), Is.EqualTo("Healthy"));
    }

    [Test]
    public async Task GetHealth_RabbitMqNotReachable_ReturnsServiceUnavailable()
    {
        // Arrange: nothing listens on port 1, so the consumer never binds its queue
        using var app = new NotificationServiceFactory();
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host.UseSetting("RabbitMq:Port", "1"));
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }
}
