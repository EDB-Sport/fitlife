using MembershipService.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MembershipService.Repositories;

/// <summary>Registers MongoDB and the repositories.</summary>
public static class MongoRepositoryRegistration
{
    /// <summary>Adds one MongoDB client for the service's database, and the repositories on it.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMongoRepositories(this IServiceCollection services)
    {
        services.AddOptions<MongoDbSettings>().BindConfiguration(MongoDbSettings.Section);
        services.AddSingleton<IMongoClient>(provider => new MongoClient(ClientSettings(provider.GetRequiredService<IOptions<MongoDbSettings>>().Value)));
        services.AddSingleton(provider => provider.GetRequiredService<IMongoClient>()
            .GetDatabase(provider.GetRequiredService<IOptions<MongoDbSettings>>().Value.Database));
        services.AddSingleton<IMemberRepository, MongoMemberRepository>();
        services.AddSingleton<IMembershipRepository, MongoMembershipRepository>();
        return services;
    }

    private static MongoClientSettings ClientSettings(MongoDbSettings settings)
    {
        return new MongoClientSettings
        {
            Server = new MongoServerAddress(settings.Host, settings.Port),
            Credential = MongoCredential.CreateCredential(settings.Database, settings.Username, settings.Password),
        };
    }
}
