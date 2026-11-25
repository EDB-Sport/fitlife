using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace IdentityService.Tests.Application;

/// <summary>The demo accounts are seeded after start-up (#237), only when <c>Seed:DemoAccounts</c> is true, and a failed seed never stops the service.</summary>
public sealed class DemoAccountSeedServiceTests
{
    private static IConfiguration Configuration(string? seedFlag)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Seed:DemoAccounts"] = seedFlag,
                ["Seed:MedlemPassword"] = "change-me-demo-medlem",
                ["Seed:TraenerPassword"] = "change-me-demo-traener",
                ["Seed:AdminPassword"] = "change-me-demo-admin",
            })
            .Build();
    }

    private static DemoAccountSeedService Service(Mock<IAccountRepository> repository, IConfiguration configuration, DemoAccountSeedStatus status, CapturingLogger logger)
    {
        var seeder = new DemoAccountSeeder(repository.Object, new PasswordHasher<Account>(), configuration);
        return new DemoAccountSeedService(new Lazy<DemoAccountSeeder>(() => seeder), status, configuration, logger);
    }

    private static async Task RunAsync(DemoAccountSeedService service)
    {
        await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
        if (service.ExecuteTask is Task running)
        {
            await running.ConfigureAwait(false);
        }

        await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [TestCase(null)]
    [TestCase("false")]
    public async Task Start_WithoutTheSeedFlag_DoesNotCallTheRepository(string? seedFlag)
    {
        // Arrange
        var repository = new Mock<IAccountRepository>();
        using var logger = new CapturingLogger();
        using DemoAccountSeedService service = Service(repository, Configuration(seedFlag), new DemoAccountSeedStatus(), logger);

        // Act
        await RunAsync(service).ConfigureAwait(false);

        // Assert
        repository.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Start_WithTheSeedFlag_SeedsAndMarksTheSeedFinished()
    {
        // Arrange
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        repository.Setup(accounts => accounts.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var status = new DemoAccountSeedStatus();
        using var logger = new CapturingLogger();
        using DemoAccountSeedService service = Service(repository, Configuration("true"), status, logger);

        // Act
        await RunAsync(service).ConfigureAwait(false);

        // Assert
        repository.Verify(accounts => accounts.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        Assert.That(status.HasFinished, Is.True);
    }

    [Test]
    public async Task Start_WhenTheSeedFails_LogsAnErrorAndKeepsRunning()
    {
        // Arrange
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("No MongoDB server answered."));
        var status = new DemoAccountSeedStatus();
        using var logger = new CapturingLogger();
        using DemoAccountSeedService service = Service(repository, Configuration("true"), status, logger);

        // Act
        await RunAsync(service).ConfigureAwait(false);

        // Assert
        Assert.That(service.ExecuteTask?.IsCompletedSuccessfully, Is.True);
        Assert.That(logger.Entries, Has.One.Matches<(LogLevel Level, Exception? Exception)>(entry => entry.Level == LogLevel.Error && entry.Exception is TimeoutException));
        Assert.That(status.HasFinished, Is.False);
    }

    [Test]
    public void Status_BeforeItIsMarked_HasNotFinished()
    {
        // Arrange & Act
        var status = new DemoAccountSeedStatus();

        // Assert
        Assert.That(status.HasFinished, Is.False);
        Assert.That(status.Finished.IsCompleted, Is.False);
    }

    /// <summary>Keeps the level and the exception of every log line.</summary>
    private sealed class CapturingLogger : ILogger<DemoAccountSeedService>, IDisposable
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception));
        }

        public void Dispose()
        {
            // Nothing to release: the entries live as long as the test.
        }
    }
}
