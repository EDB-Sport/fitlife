using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;

namespace NotificationService.IntegrationTests;

/// <summary>One RabbitMQ and one MongoDB for the whole test run, the images compose uses (deploy/compose/infrastructure.yml).</summary>
[SetUpFixture]
public sealed class TestInfrastructure
{
    /// <summary>Gets the RabbitMQ the service consumes from.</summary>
    public static RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.4.1-management").Build();

    /// <summary>
    /// Gets the MongoDB the service stores its notifications in. MONGO_IMAGE overrides the image, as in compose, for
    /// Docker hosts where MongoDB 8 won't start (deploy/compose/README.md).
    /// </summary>
    public static MongoDbContainer MongoDb { get; } = new MongoDbBuilder(
        new ConfigurationBuilder().AddEnvironmentVariables().Build()["MONGO_IMAGE"] ?? "mongo:8.0.38").Build();

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        await Task.WhenAll(RabbitMq.StartAsync(), MongoDb.StartAsync()).ConfigureAwait(false);
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        await RabbitMq.DisposeAsync().ConfigureAwait(false);
        await MongoDb.DisposeAsync().ConfigureAwait(false);
    }
}
