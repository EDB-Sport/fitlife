using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>One MongoDB container for every test in this namespace. Each test uses its own database, so no test sees another's data.</summary>
[SetUpFixture]
public sealed class MongoDbContainerSetUp
{
    private static MongoDbContainer? Container;

    /// <summary>The connection string of the running container.</summary>
    internal static string ConnectionString => Container?.GetConnectionString() ?? throw new InvalidOperationException("The MongoDB container hasn't started.");

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
