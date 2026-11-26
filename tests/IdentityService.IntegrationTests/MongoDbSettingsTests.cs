using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.IntegrationTests;

/// <summary>The service's own database and user, as compose creates them (deploy/compose/mongodb/init, #233).</summary>
public sealed class MongoDbSettingsTests
{
    [Test]
    public void AppSettings_NameTheIdentityDatabaseAndTheIdentitySvcUser()
    {
        // Arrange
        using var factory = new TestKeyFactory();

        // Act
        IConfiguration configuration = factory.Services.GetRequiredService<IConfiguration>();

        // Assert
        Assert.That(configuration["MongoDb:Database"], Is.EqualTo("identity"));
        Assert.That(configuration["MongoDb:Username"], Is.EqualTo("identity-svc"));
    }
}
