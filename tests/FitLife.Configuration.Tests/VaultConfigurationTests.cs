using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace FitLife.Configuration.Tests;

/// <summary>AddVault loads a service's secret from Vault's KV v2 engine into IConfiguration (#17, course rule #9).</summary>
public sealed class VaultConfigurationTests
{
    private const string SecretJson = """
        {
          "data": {
            "data": {
              "MongoDb:Password": "db-secret",
              "RabbitMq:Username": "fitlife",
              "Jwt:SigningKey": "signing-key"
            },
            "metadata": { "version": 1 }
          }
        }
        """;

    private static ConfigurationManager Settings(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)));
        return configuration;
    }

    private static ConfigurationManager VaultSettings()
    {
        return Settings(("Vault:Address", "http://vault:8200"), ("Vault:Token", "root-token"), ("Vault:Path", "booking-svc"));
    }

    [Test]
    public void AddVault_WithoutAnAddress_LoadsNothingAndSendsNoRequest()
    {
        // Arrange
        using var vault = new FakeVault(HttpStatusCode.OK, SecretJson);
        using ConfigurationManager configuration = Settings(("Vault:Path", "booking-svc"));

        // Act
        configuration.AddVault(vault);

        // Assert
        Assert.That(vault.Request, Is.Null);
        Assert.That(configuration["MongoDb:Password"], Is.Null);
    }

    [Test]
    public void AddVault_WithAnAddress_ReadsTheSecretAsConfigurationKeys()
    {
        // Arrange
        using var vault = new FakeVault(HttpStatusCode.OK, SecretJson);
        using ConfigurationManager configuration = VaultSettings();

        // Act
        configuration.AddVault(vault);

        // Assert
        Assert.That(configuration["MongoDb:Password"], Is.EqualTo("db-secret"));
        Assert.That(configuration["RabbitMq:Username"], Is.EqualTo("fitlife"));
        Assert.That(configuration.GetSection("Jwt")["SigningKey"], Is.EqualTo("signing-key"));
        Assert.That(configuration["metadata:version"], Is.Null);
    }

    [Test]
    public void AddVault_WithAnAddress_GetsTheSecretWithTheToken()
    {
        // Arrange
        using var vault = new FakeVault(HttpStatusCode.OK, SecretJson);

        using ConfigurationManager configuration = VaultSettings();

        // Act
        configuration.AddVault(vault);

        // Assert
        Assert.That(vault.Request?.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(vault.Request?.RequestUri, Is.EqualTo(new Uri("http://vault:8200/v1/secret/data/booking-svc")));
        Assert.That(vault.Request?.Headers.GetValues("X-Vault-Token").Single(), Is.EqualTo("root-token"));
    }

    [TestCase("http://vault:8200/", "secret", "http://vault:8200/v1/secret/data/booking-svc")]
    [TestCase("http://vault:8200/proxy", "kv", "http://vault:8200/proxy/v1/kv/data/booking-svc")]
    public void AddVault_WithAMountOrAPathInTheAddress_BuildsTheSecretUrl(string address, string mount, string expected)
    {
        // Arrange
        using var vault = new FakeVault(HttpStatusCode.OK, SecretJson);
        using ConfigurationManager configuration = Settings(
            ("Vault:Address", address), ("Vault:Token", "root-token"), ("Vault:Mount", mount), ("Vault:Path", "booking-svc"));

        // Act
        configuration.AddVault(vault);

        // Assert
        Assert.That(vault.Request?.RequestUri, Is.EqualTo(new Uri(expected)));
    }

    [Test]
    public void AddVault_WhenVaultRefuses_ThrowsWithTheStatusAndPathButNotTheToken()
    {
        // Arrange
        using var vault = new FakeVault(HttpStatusCode.Forbidden, """{ "errors": ["permission denied"] }""");
        using ConfigurationManager configuration = VaultSettings();

        // Act
        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => configuration.AddVault(vault));

        // Assert
        Assert.That(exception?.Message, Does.Contain("403").And.Contain("secret/booking-svc"));
        Assert.That(exception?.Message, Does.Not.Contain("root-token"));
    }

    [TestCase("Vault:Token")]
    [TestCase("Vault:Path")]
    public void AddVault_WithAnAddressButAMissingSetting_ThrowsAndNamesTheSetting(string missing)
    {
        // Arrange
        using var vault = new FakeVault(HttpStatusCode.OK, SecretJson);
        using ConfigurationManager configuration = VaultSettings();
        configuration[missing] = string.Empty;

        // Act
        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => configuration.AddVault(vault));

        // Assert
        Assert.That(exception?.Message, Does.Contain(missing));
        Assert.That(vault.Request, Is.Null);
    }

    // Answers every request with one canned response, and keeps the request
    private sealed class FakeVault(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request, cancellationToken));
        }
    }
}
