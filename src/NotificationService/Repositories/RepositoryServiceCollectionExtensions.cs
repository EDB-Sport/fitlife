using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace NotificationService.Repositories;

/// <summary>Registers the database, here because MongoDB types stay in this namespace (TEAM001).</summary>
public static class RepositoryServiceCollectionExtensions
{
    /// <summary>The MongoDB client from the <c>MongoDb</c> configuration section, and the repository.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The service's configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddNotificationRepository(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbOptions>(configuration.GetSection(MongoDbOptions.Section));
        services.AddSingleton<IMongoClient>(provider =>
        {
            MongoDbOptions options = provider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            return new MongoClient(new MongoClientSettings
            {
                Server = new MongoServerAddress(options.Host, options.Port),
                Credential = MongoCredential.CreateCredential(options.AuthDatabase ?? options.Database, options.Username, options.Password),
            });
        });
        services.AddSingleton(provider => provider.GetRequiredService<IMongoClient>()
            .GetDatabase(provider.GetRequiredService<IOptions<MongoDbOptions>>().Value.Database));
        services.AddSingleton<INotificationRepository, MongoNotificationRepository>();
        return services;
    }
}
