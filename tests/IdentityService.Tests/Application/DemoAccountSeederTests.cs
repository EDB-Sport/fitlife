using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;

namespace IdentityService.Tests.Application;

/// <summary>The demo accounts (#237): one account per role, with passwords from configuration.</summary>
public sealed class DemoAccountSeederTests
{
    private const string MedlemPassword = "change-me-demo-medlem";
    private const string TraenerPassword = "change-me-demo-traener";
    private const string AdminPassword = "change-me-demo-admin";

    private static readonly string[] AllDemoEmails = ["medlem@fitlife.example", "traener@fitlife.example", "admin@fitlife.example"];
    private static readonly string[] DemoEmailsButTraener = ["medlem@fitlife.example", "admin@fitlife.example"];

    private readonly PasswordHasher<Account> _hasher = new();

    private static IConfiguration Configuration(string? skippedKey = null)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Seed:MedlemPassword"] = MedlemPassword,
            ["Seed:TraenerPassword"] = TraenerPassword,
            ["Seed:AdminPassword"] = AdminPassword,
        };
        if (skippedKey is not null)
        {
            settings[skippedKey] = null;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private DemoAccountSeeder Seeder(Mock<IAccountRepository> repository, IConfiguration configuration)
    {
        return new DemoAccountSeeder(repository.Object, _hasher, configuration);
    }

    [Test]
    public async Task SeedAsync_WhenNoDemoAccountExists_AddsOneAccountPerRole()
    {
        // Arrange
        var added = new List<Account>();
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        repository.Setup(accounts => accounts.AddAsync(Capture.In(added), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await Seeder(repository, Configuration()).SeedAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(added.Select(account => account.Email), Is.EqualTo(AllDemoEmails));
        Assert.That(added.Select(account => account.Roles.Single()), Is.EqualTo(new[] { Role.Medlem, Role.Traener, Role.Admin }));
    }

    [Test]
    public async Task SeedAsync_WhenNoDemoAccountExists_HashesThePasswordFromConfiguration()
    {
        // Arrange
        var added = new List<Account>();
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        repository.Setup(accounts => accounts.AddAsync(Capture.In(added), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await Seeder(repository, Configuration()).SeedAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        string[] passwords = [MedlemPassword, TraenerPassword, AdminPassword];
        Assert.That(added, Has.Count.EqualTo(3));
        for (int index = 0; index < added.Count; index++)
        {
            Assert.That(_hasher.VerifyHashedPassword(added[index], added[index].PasswordHash, passwords[index]), Is.EqualTo(PasswordVerificationResult.Success));
            Assert.That(added[index].PasswordHash, Is.Not.EqualTo(passwords[index]));
        }
    }

    [Test]
    public async Task SeedAsync_WhenTheDemoAccountsExist_ChangesNothing()
    {
        // Arrange
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string email, CancellationToken _) => Account.Create(email, "AQAAAAIAAYagAAAAEhash"));

        // Act
        await Seeder(repository, Configuration()).SeedAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        repository.Verify(accounts => accounts.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task SeedAsync_WhenOneDemoAccountExists_AddsOnlyTheOthers()
    {
        // Arrange
        var added = new List<Account>();
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        repository.Setup(accounts => accounts.FindByEmailAsync("traener@fitlife.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account.Create("traener@fitlife.example", "AQAAAAIAAYagAAAAEhash", Role.Traener));
        repository.Setup(accounts => accounts.AddAsync(Capture.In(added), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await Seeder(repository, Configuration()).SeedAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(added.Select(account => account.Email), Is.EqualTo(DemoEmailsButTraener));
    }

    [TestCase("Seed:MedlemPassword")]
    [TestCase("Seed:TraenerPassword")]
    [TestCase("Seed:AdminPassword")]
    public void SeedAsync_WithAMissingPassword_ThrowsAndNamesTheSetting(string key)
    {
        // Arrange
        var repository = new Mock<IAccountRepository>();
        DemoAccountSeeder seeder = Seeder(repository, Configuration(key));

        // Act & Assert
        Assert.That(() => seeder.SeedAsync(CancellationToken.None), Throws.InvalidOperationException.With.Message.Contains(key));
        repository.VerifyNoOtherCalls();
    }
}
