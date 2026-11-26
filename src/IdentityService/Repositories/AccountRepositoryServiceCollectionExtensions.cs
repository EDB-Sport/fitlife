using System.Globalization;
using IdentityService.Application;
using MongoDB.Driver;

namespace IdentityService.Repositories;

/// <summary>Registers the MongoDB client and <see cref="IAccountRepository"/>. Nothing connects until the repository is first used.</summary>
public static class AccountRepositoryServiceCollectionExtensions
{
    /// <summary>
    /// Builds the client from <c>MongoDb:Host</c>, <c>MongoDb:Port</c>, <c>MongoDb:Database</c>, <c>MongoDb:Username</c> and
    /// <c>MongoDb:Password</c> (from Vault). The user logs in to its own database, as compose creates it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddMongoAccountRepository(this IServiceCollection services)
    {
        services.AddSingleton<IMongoClient>(provider => new MongoClient(Settings(provider.GetRequiredService<IConfiguration>())));
        services.AddSingleton<IAccountRepository>(provider =>
        {
            string database = Required(provider.GetRequiredService<IConfiguration>(), "MongoDb:Database");
            return new MongoAccountRepository(provider.GetRequiredService<IMongoClient>().GetDatabase(database));
        });
        return services;
    }

    private static MongoClientSettings Settings(IConfiguration configuration)
    {
        string host = Required(configuration, "MongoDb:Host");
        string port = Required(configuration, "MongoDb:Port");
        if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int portNumber))
        {
            throw new InvalidOperationException("MongoDb:Port must be a number.");
        }

        string database = Required(configuration, "MongoDb:Database");
        string username = Required(configuration, "MongoDb:Username");
        string password = Required(configuration, "MongoDb:Password");
        return new MongoClientSettings
        {
            Server = new MongoServerAddress(host, portNumber),
            Credential = MongoCredential.CreateCredential(database, username, password),
        };
    }

    private static string Required(IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Set {key}.");
        }

        return value;
    }
}
