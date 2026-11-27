using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;

namespace FitLife.Flows.IntegrationTests;

/// <summary>One RabbitMQ and one MongoDB for every flow test, and the token key both services trust (#192).</summary>
[SetUpFixture]
public sealed class FlowInfrastructure
{
    private const string SigningKeyVariable = "Jwt__SigningKey";

    /// <summary>The broker both services use.</summary>
    public static RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.4.1-management").Build();

    /// <summary>One server; each service has its own database on it, as in compose.</summary>
    public static MongoDbContainer MongoDb { get; } = new MongoDbBuilder(
        new ConfigurationBuilder().AddEnvironmentVariables().Build()["MONGO_IMAGE"] ?? "mongo:8.0.38").Build();

    /// <summary>Starts the containers and sets the signing key for both hosts.</summary>
    /// <returns>A task that completes when both containers run.</returns>
    [OneTimeSetUp]
    public async Task StartAsync()
    {
        Environment.SetEnvironmentVariable(SigningKeyVariable, FlowTokens.SigningKey);
        await Task.WhenAll(RabbitMq.StartAsync(), MongoDb.StartAsync()).ConfigureAwait(false);
    }

    /// <summary>Stops the containers.</summary>
    /// <returns>A task that completes when both are gone.</returns>
    [OneTimeTearDown]
    public async Task StopAsync()
    {
        Environment.SetEnvironmentVariable(SigningKeyVariable, null);
        await RabbitMq.DisposeAsync().ConfigureAwait(false);
        await MongoDb.DisposeAsync().ConfigureAwait(false);
    }
}
