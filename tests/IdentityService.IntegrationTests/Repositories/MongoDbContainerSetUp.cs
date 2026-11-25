using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;

namespace IdentityService.IntegrationTests.Repositories;

/// <summary>One MongoDB container for every test in this namespace. Each test uses its own database, so no test sees another's data.</summary>
[SetUpFixture]
public sealed class MongoDbContainerSetUp
{
    private static MongoDbContainer? Container;

    private static MongoDbContainer Running => Container ?? throw new InvalidOperationException("The MongoDB container hasn't started.");

    /// <summary>The root user's connection string, to create the service's user.</summary>
    internal static string RootConnectionString => Running.GetConnectionString();

    /// <summary>The host the container is reached on.</summary>
    internal static string Host => Running.Hostname;

    /// <summary>The port MongoDB's 27017 is mapped to.</summary>
    internal static int Port => Running.GetMappedPublicPort(27017);

    /// <summary>
    /// Starts the container. The image is the one compose uses (deploy/compose/infrastructure.yml), and MONGO_IMAGE overrides it
    /// the same way, for a Docker whose kernel MongoDB 8 refuses (deploy/compose/README.md).
    /// </summary>
    [OneTimeSetUp]
    public async Task StartAsync()
    {
        IConfiguration environment = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        Container = new MongoDbBuilder(environment["MONGO_IMAGE"] ?? "mongo:8.0.38").Build();
        await Container.StartAsync().ConfigureAwait(false);
    }

    /// <summary>Stops and removes the container.</summary>
    [OneTimeTearDown]
    public async Task StopAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
