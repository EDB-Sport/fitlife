using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace FitLife.Configuration.Tests;

/// <summary>AddVaultAsync loads a service's secret from Vault's KV v2 engine into IConfiguration (#17, course rule #9).</summary>
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

    private static HttpResponseMessage Answer(HttpStatusCode status, string json = "{}")
    {
        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    // Moves the fake clock on by one second, each time after the code has sent its next request, until the load has
    // finished. No test waits in real time.
    private static async Task RunWithClockAsync(Task load, FakeTimeProvider time, FakeVault vault)
    {
        for (int second = 0; second < 600 && !load.IsCompleted; second++)
        {
            int sent = vault.Requests.Count;
            time.Advance(TimeSpan.FromSeconds(1));
            for (int spin = 0; spin < 100_000 && !load.IsCompleted && vault.Requests.Count == sent; spin++)
            {
                await Task.Yield();
            }
        }

        await load.ConfigureAwait(false);
    }

    [Test]
    public async Task AddVaultAsync_WithoutAnAddress_LoadsNothingAndSendsNoRequest()
    {
        // Arrange
        using var vault = new FakeVault(_ => Answer(HttpStatusCode.OK, SecretJson));
        using ConfigurationManager configuration = Settings(("Vault:Path", "booking-svc"));

        // Act
        await configuration.AddVaultAsync(vault).ConfigureAwait(false);

        // Assert
        Assert.That(vault.Requests, Is.Empty);
        Assert.That(configuration["MongoDb:Password"], Is.Null);
    }

    [Test]
    public async Task AddVaultAsync_WithAnAddress_ReadsTheSecretAsConfigurationKeys()
    {
        // Arrange
        using var vault = new FakeVault(_ => Answer(HttpStatusCode.OK, SecretJson));
        using ConfigurationManager configuration = VaultSettings();

        // Act
        await configuration.AddVaultAsync(vault).ConfigureAwait(false);

        // Assert
        Assert.That(configuration["MongoDb:Password"], Is.EqualTo("db-secret"));
        Assert.That(configuration["RabbitMq:Username"], Is.EqualTo("fitlife"));
        Assert.That(configuration.GetSection("Jwt")["SigningKey"], Is.EqualTo("signing-key"));
        Assert.That(configuration["metadata:version"], Is.Null);
    }

    [Test]
    public async Task AddVaultAsync_WithAnAddress_GetsTheSecretWithTheToken()
    {
        // Arrange
        using var vault = new FakeVault(_ => Answer(HttpStatusCode.OK, SecretJson));
        using ConfigurationManager configuration = VaultSettings();

        // Act
        await configuration.AddVaultAsync(vault).ConfigureAwait(false);

        // Assert
        Assert.That(vault.Requests, Has.Count.EqualTo(1));
        Assert.That(vault.Requests.First().Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(vault.Requests.First().Uri, Is.EqualTo(new Uri("http://vault:8200/v1/secret/data/booking-svc")));
        Assert.That(vault.Requests.First().Token, Is.EqualTo("root-token"));
    }

    [TestCase("http://vault:8200/", "secret", "http://vault:8200/v1/secret/data/booking-svc")]
    [TestCase("http://vault:8200/proxy", "kv", "http://vault:8200/proxy/v1/kv/data/booking-svc")]
    public async Task AddVaultAsync_WithAMountOrAPathInTheAddress_BuildsTheSecretUrl(string address, string mount, string expected)
    {
        // Arrange
        using var vault = new FakeVault(_ => Answer(HttpStatusCode.OK, SecretJson));
        using ConfigurationManager configuration = Settings(
            ("Vault:Address", address), ("Vault:Token", "root-token"), ("Vault:Mount", mount), ("Vault:Path", "booking-svc"));

        // Act
        await configuration.AddVaultAsync(vault).ConfigureAwait(false);

        // Assert
        Assert.That(vault.Requests.First().Uri, Is.EqualTo(new Uri(expected)));
    }

    [Test]
    public void AddVaultAsync_WhenVaultRefusesTheToken_ThrowsAtOnceWithTheStatusAndPathButNotTheToken()
    {
        // Arrange
        using var vault = new FakeVault(_ => Answer(HttpStatusCode.Forbidden, """{ "errors": ["permission denied"] }"""));
        using ConfigurationManager configuration = VaultSettings();
        var time = new FakeTimeProvider();

        // Act
        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await configuration.AddVaultAsync(vault, time).ConfigureAwait(false));

        // Assert
        Assert.That(exception?.Message, Does.Contain("403").And.Contain("secret/booking-svc"));
        Assert.That(exception?.Message, Does.Not.Contain("root-token"));
        Assert.That(vault.Requests, Has.Count.EqualTo(1));
    }

    [TestCase("Vault:Token")]
    [TestCase("Vault:Path")]
    public void AddVaultAsync_WithAnAddressButAMissingSetting_ThrowsAndNamesTheSetting(string missing)
    {
        // Arrange
        using var vault = new FakeVault(_ => Answer(HttpStatusCode.OK, SecretJson));
        using ConfigurationManager configuration = VaultSettings();
        configuration[missing] = string.Empty;

        // Act
        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await configuration.AddVaultAsync(vault).ConfigureAwait(false));

        // Assert
        Assert.That(exception?.Message, Does.Contain(missing));
        Assert.That(vault.Requests, Is.Empty);
    }

    // Compose and Azure start Vault and the service together, and Vault is seeded a few seconds later
    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public async Task AddVaultAsync_WhileTheSecretIsNotThereYet_TriesAgainUntilItIs(HttpStatusCode notYet)
    {
        // Arrange
        int attempts = 0;
        using var vault = new FakeVault(_ =>
        {
            attempts++;
            if (attempts < 4)
            {
                return Answer(notYet);
            }

            return Answer(HttpStatusCode.OK, SecretJson);
        });
        using ConfigurationManager configuration = VaultSettings();
        var time = new FakeTimeProvider();

        // Act
        await RunWithClockAsync(configuration.AddVaultAsync(vault, time), time, vault).ConfigureAwait(false);

        // Assert
        Assert.That(configuration["MongoDb:Password"], Is.EqualTo("db-secret"));
        Assert.That(vault.Requests, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task AddVaultAsync_WhileVaultDoesNotAnswer_TriesAgainUntilItDoes()
    {
        // Arrange
        int attempts = 0;
        using var vault = new FakeVault(_ =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new HttpRequestException("Connection refused (vault:8200)");
            }

            return Answer(HttpStatusCode.OK, SecretJson);
        });
        using ConfigurationManager configuration = VaultSettings();
        var time = new FakeTimeProvider();

        // Act
        await RunWithClockAsync(configuration.AddVaultAsync(vault, time), time, vault).ConfigureAwait(false);

        // Assert
        Assert.That(configuration["MongoDb:Password"], Is.EqualTo("db-secret"));
    }

    [Test]
    public void AddVaultAsync_WhenTheSecretNeverComes_GivesUpAfterVaultWaitSeconds()
    {
        // Arrange
        using var vault = new FakeVault(_ => Answer(HttpStatusCode.NotFound));
        using ConfigurationManager configuration = VaultSettings();
        configuration["Vault:WaitSeconds"] = "10";
        var time = new FakeTimeProvider();
        DateTimeOffset start = time.GetUtcNow();

        // Act
        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await RunWithClockAsync(configuration.AddVaultAsync(vault, time), time, vault).ConfigureAwait(false));

        // Assert
        Assert.That(exception?.Message, Does.Contain("404").And.Contain("10 s"));
        Assert.That(time.GetUtcNow() - start, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(10)).And.LessThan(TimeSpan.FromSeconds(12)));
        Assert.That(vault.Requests, Has.Count.GreaterThan(1));
    }

    private sealed record SentRequest(HttpMethod Method, Uri? Uri, string? Token);

    // Answers each request with the response the test gives, and keeps what was sent
    private sealed class FakeVault(Func<int, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public ConcurrentQueue<SentRequest> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? token = null;
            if (request.Headers.TryGetValues("X-Vault-Token", out IEnumerable<string>? values))
            {
                token = values.Single();
            }

            Requests.Enqueue(new SentRequest(request.Method, request.RequestUri, token));
            return Task.FromResult(answer(Requests.Count));
        }
    }
}
