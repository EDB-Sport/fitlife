using IdentityService.Application;
using IdentityService.Domain;
using IdentityService.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IdentityService.IntegrationTests.Repositories;

/// <summary>
/// Accounts stored in MongoDB (#233). Like compose (deploy/compose/mongodb/init), each test first creates the service's user
/// with readWrite on its own database, so the repository logs in as identity-svc does.
/// </summary>
public sealed class MongoAccountRepositoryTests
{
    private const string Username = "identity-svc";
    private const string PasswordHash = "AQAAAAIAAYagAAAAEhash";

    private ServiceProvider? _provider;

    private async Task<IAccountRepository> RepositoryAsync()
    {
        string database = $"identity-{Guid.NewGuid():N}";
        string password = $"made-up-{Guid.NewGuid():N}";
        using (var root = new MongoClient(MongoDbContainerSetUp.RootConnectionString))
        {
            var createUser = new BsonDocument
            {
                { "createUser", Username },
                { "pwd", password },
                { "roles", new BsonArray { new BsonDocument { { "role", "readWrite" }, { "db", database } } } },
            };
            await root.GetDatabase(database).RunCommandAsync<BsonDocument>(createUser).ConfigureAwait(false);
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["MongoDb:Host"] = MongoDbContainerSetUp.Host,
                ["MongoDb:Port"] = MongoDbContainerSetUp.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["MongoDb:Database"] = database,
                ["MongoDb:Username"] = Username,
                ["MongoDb:Password"] = password,
            })
            .Build();
        _provider = new ServiceCollection().AddSingleton(configuration).AddMongoAccountRepository().BuildServiceProvider();
        return _provider.GetRequiredService<IAccountRepository>();
    }

    [TearDown]
    public async Task DisposeProviderAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task FindByEmailAsync_AfterAddAsync_ReturnsTheSameAccount()
    {
        // Arrange
        IAccountRepository repository = await RepositoryAsync().ConfigureAwait(false);
        var account = Account.Create("anna@fitlife.example", PasswordHash);
        await repository.AddAsync(account, CancellationToken.None).ConfigureAwait(false);

        // Act
        Account? found = await repository.FindByEmailAsync("anna@fitlife.example", CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(found?.Id, Is.EqualTo(account.Id));
        Assert.That(found?.Email, Is.EqualTo(account.Email));
        Assert.That(found?.PasswordHash, Is.EqualTo(PasswordHash));
        Assert.That(found?.Roles, Is.EqualTo(new[] { Role.Medlem }));
    }

    [Test]
    public async Task AddAsync_WithANewEmail_AnswersThatTheEmailWasFree()
    {
        // Arrange
        IAccountRepository repository = await RepositoryAsync().ConfigureAwait(false);

        // Act
        bool added = await repository.AddAsync(Account.Create("anna@fitlife.example", PasswordHash), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(added, Is.True);
    }

    [Test]
    public async Task FindByEmailAsync_WithAnUnknownEmail_ReturnsNull()
    {
        // Arrange
        IAccountRepository repository = await RepositoryAsync().ConfigureAwait(false);
        await repository.AddAsync(Account.Create("anna@fitlife.example", PasswordHash), CancellationToken.None).ConfigureAwait(false);

        // Act
        Account? found = await repository.FindByEmailAsync("bo@fitlife.example", CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task AddAsync_WithTheSameEmailInAnotherLetterCase_IsRejected()
    {
        // Arrange
        IAccountRepository repository = await RepositoryAsync().ConfigureAwait(false);
        await repository.AddAsync(Account.Create("anna@fitlife.example", PasswordHash), CancellationToken.None).ConfigureAwait(false);

        // Act
        bool added = await repository.AddAsync(Account.Create("Anna@FitLife.Example", PasswordHash), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(added, Is.False);
    }

    [Test]
    public async Task FindByEmailAsync_WithAnotherLetterCase_ReturnsTheAccount()
    {
        // Arrange
        IAccountRepository repository = await RepositoryAsync().ConfigureAwait(false);
        var account = Account.Create("anna@fitlife.example", PasswordHash);
        await repository.AddAsync(account, CancellationToken.None).ConfigureAwait(false);

        // Act
        Account? found = await repository.FindByEmailAsync("ANNA@fitlife.example", CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(found?.Id, Is.EqualTo(account.Id));
    }
}
