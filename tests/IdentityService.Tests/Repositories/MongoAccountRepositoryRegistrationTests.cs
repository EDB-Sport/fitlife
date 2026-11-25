using IdentityService.Application;
using IdentityService.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Tests.Repositories;

/// <summary>The repository is built from the MongoDb:* settings. Registering it connects to nothing (#233).</summary>
public sealed class MongoAccountRepositoryRegistrationTests
{
    private static Dictionary<string, string?> AllSettings() => new(StringComparer.Ordinal)
    {
        ["MongoDb:Host"] = "mongodb",
        ["MongoDb:Port"] = "27017",
        ["MongoDb:Database"] = "identity",
        ["MongoDb:Username"] = "identity-svc",
        ["MongoDb:Password"] = "made-up",
    };

    private static ServiceProvider Provider(Dictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddSingleton(configuration).AddMongoAccountRepository().BuildServiceProvider();
    }

    [Test]
    public void AddMongoAccountRepository_WithEverySetting_ResolvesTheRepositoryWithoutConnecting()
    {
        // Arrange
        using ServiceProvider provider = Provider(AllSettings());

        // Act
        IAccountRepository repository = provider.GetRequiredService<IAccountRepository>();

        // Assert
        Assert.That(repository, Is.InstanceOf<MongoAccountRepository>());
    }

    [TestCase("MongoDb:Host")]
    [TestCase("MongoDb:Port")]
    [TestCase("MongoDb:Database")]
    [TestCase("MongoDb:Username")]
    [TestCase("MongoDb:Password")]
    public void AddMongoAccountRepository_WithoutASetting_ThrowsAndNamesTheSetting(string setting)
    {
        // Arrange
        Dictionary<string, string?> settings = AllSettings();
        settings.Remove(setting);
        using ServiceProvider provider = Provider(settings);

        // Act & Assert
        Assert.That(() => provider.GetRequiredService<IAccountRepository>(), Throws.InvalidOperationException.With.Message.Contains(setting));
    }

    [Test]
    public void AddMongoAccountRepository_WithAPortThatIsNotANumber_ThrowsAndNamesTheSetting()
    {
        // Arrange
        Dictionary<string, string?> settings = AllSettings();
        settings["MongoDb:Port"] = "mongo";
        using ServiceProvider provider = Provider(settings);

        // Act & Assert
        Assert.That(() => provider.GetRequiredService<IAccountRepository>(), Throws.InvalidOperationException.With.Message.Contains("MongoDb:Port"));
    }
}
