using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace BookingService.Repositories;

/// <summary>Registers the MongoDB client and the repositories. Nothing connects until a repository is first used.</summary>
public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    /// Reads <c>MongoDb:ConnectionString</c> (without a password), <c>MongoDb:Password</c> (from Vault, #17) and
    /// <c>MongoDb:Database</c> from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Ids are stored as standard UUIDs. One registration per process; the tests start the app many times.
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        services.AddSingleton<IMongoClient>(provider => new MongoClient(Settings(provider.GetRequiredService<IConfiguration>())));
        services.AddSingleton(provider =>
        {
            string database = provider.GetRequiredService<IConfiguration>()["MongoDb:Database"]
                ?? throw new InvalidOperationException("Set MongoDb:Database.");
            return provider.GetRequiredService<IMongoClient>().GetDatabase(database);
        });
        services.AddScoped<IClassSessionRepository, ClassSessionRepository>();
        return services;
    }

    private static MongoClientSettings Settings(IConfiguration configuration)
    {
        string connectionString = configuration["MongoDb:ConnectionString"]
            ?? throw new InvalidOperationException("Set MongoDb:ConnectionString.");
        // The URL names the user only; the password comes from its own setting (Vault). Put it in before the URL is parsed into
        // settings, which reject a user without a password.
        var url = new MongoUrlBuilder(connectionString);
        string? password = configuration["MongoDb:Password"];
        if (!string.IsNullOrEmpty(password))
        {
            url.Password = password;
        }

        var settings = MongoClientSettings.FromUrl(url.ToMongoUrl());
        return settings;
    }
}
