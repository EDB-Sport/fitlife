using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IdentityService.IntegrationTests;

/// <summary>The /health endpoint that docker compose --wait and the Azure probes use.</summary>
public sealed class HealthEndpointTests
{
    [Test]
    public async Task GetHealth_ReturnsOkAndHealthy()
    {
        // Arrange
        using var factory = new TestKeyFactory();
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync().ConfigureAwait(false), Is.EqualTo("Healthy"));
    }
}
