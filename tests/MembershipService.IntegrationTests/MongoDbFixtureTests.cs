using Microsoft.Extensions.Configuration;

namespace MembershipService.IntegrationTests;

/// <summary>The fixture's image is compose's, and MONGO_IMAGE overrides it as in the other services' fixtures (#588).</summary>
public sealed class MongoDbFixtureTests
{
    [Test]
    public async Task Image_WithMongoImageSet_IsThatImage()
    {
        // Arrange: as on a Docker whose kernel MongoDB 8 refuses (deploy/compose/README.md)
        IConfiguration environment = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["MONGO_IMAGE"] = "mongo:7.0" })
            .Build();

        // Act
        await using var fixture = new MongoDbFixture(environment);

        // Assert
        Assert.That(fixture.Image, Is.EqualTo("mongo:7.0"));
    }

    [Test]
    public async Task Image_WithoutMongoImage_IsComposesImage()
    {
        // Arrange
        IConfiguration environment = new ConfigurationBuilder().Build();

        // Act
        await using var fixture = new MongoDbFixture(environment);

        // Assert
        Assert.That(fixture.Image, Is.EqualTo("mongo:8.0.38"));
    }
}
