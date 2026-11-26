using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NotificationService.IntegrationTests;

/// <summary>NotificationService loads secret/notification-svc from Vault at startup and reads it through IConfiguration (#259).</summary>
public sealed class VaultSecretsTests
{
    [TestCase("MongoDb:Password", "notification-db-secret")]
    [TestCase("RabbitMq:Username", "notification-rabbitmq-user")]
    [TestCase("RabbitMq:Password", "notification-rabbitmq-secret")]
    public async Task Start_WithVaultSettings_ReadsTheNotificationSecretThroughConfiguration(string key, string value)
    {
        // Arrange: a stand-in for Vault's KV v2 API that answers only with the right path and token, with the keys vault/seed.sh writes
        await using WebApplication vault = WebApplication.CreateBuilder().Build();
        vault.Urls.Add("http://127.0.0.1:0");
        vault.MapGet("/v1/secret/data/notification-svc", (HttpRequest request) =>
            string.Equals(request.Headers["X-Vault-Token"], "test-root-token", StringComparison.Ordinal)
                ? Results.Json(new
                {
                    data = new
                    {
                        data = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["MongoDb:Password"] = "notification-db-secret",
                            ["RabbitMq:Username"] = "notification-rabbitmq-user",
                            ["RabbitMq:Password"] = "notification-rabbitmq-secret",
                        },
                    },
                })
                : Results.StatusCode(StatusCodes.Status403Forbidden));
        await vault.StartAsync().ConfigureAwait(false);
        string vaultUrl = vault.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.Single();

        using var app = new WebApplicationFactory<Program>();
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host
            .UseSetting("Vault:Address", vaultUrl)
            .UseSetting("Vault:Token", "test-root-token"));

        // Act
        using HttpClient client = factory.CreateClient();
        IConfiguration configuration = factory.Services.GetRequiredService<IConfiguration>();

        // Assert
        Assert.That(configuration[key], Is.EqualTo(value));
    }
}
