using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPortal.Web.IntegrationTests;

/// <summary>The portal's log lines reach Loki under the app label the Grafana dashboard and alerts filter on (#326).</summary>
public sealed class LokiSettingsTests
{
    [Test]
    public void AppSettings_LokiApp_IsMemberPortal()
    {
        // Arrange
        using var factory = new WebApplicationFactory<Program>();

        // Act
        IConfiguration configuration = factory.Services.GetRequiredService<IConfiguration>();

        // Assert
        Assert.That(configuration["Loki:App"], Is.EqualTo("member-portal"));
    }
}
