using System.Globalization;
using BookingService.Application;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace BookingService.Repositories;

/// <summary>Registers the MongoDB client and the repositories. Nothing connects until a repository is first used.</summary>
public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    /// Builds the client from <c>MongoDb:Host</c>, <c>MongoDb:Port</c>, <c>MongoDb:Database</c>, <c>MongoDb:Username</c>,
    /// <c>MongoDb:Password</c> (from Vault, #17) and the optional <c>MongoDb:AuthSource</c>, the database the login lives in
    /// (default: <c>MongoDb:Database</c>, as compose creates booking-svc). Read when the client is first resolved, not here.
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
            string database = Required(provider.GetRequiredService<IConfiguration>(), "MongoDb:Database");
            return provider.GetRequiredService<IMongoClient>().GetDatabase(database);
        });
        services.AddScoped<IClassSessionRepository, ClassSessionRepository>();
        services.AddSingleton<IReferenceDataRepository, ReferenceDataRepository>();
        services.AddSingleton<IReferenceDataWriter, ReferenceDataWriter>(); // only the demo-data seeder writes (#255)
        return services;
    }

    private static MongoClientSettings Settings(IConfiguration configuration)
    {
        string host = Required(configuration, "MongoDb:Host");
        if (!int.TryParse(Required(configuration, "MongoDb:Port"), NumberStyles.None, CultureInfo.InvariantCulture, out int port))
        {
            throw new InvalidOperationException("MongoDb:Port must be a number.");
        }

        string database = Required(configuration, "MongoDb:Database");
        string? authSource = configuration["MongoDb:AuthSource"];
        if (string.IsNullOrEmpty(authSource))
        {
            authSource = database;
        }

        var url = new MongoUrlBuilder
        {
            Server = new MongoServerAddress(host, port),
            Username = Required(configuration, "MongoDb:Username"),
            Password = Required(configuration, "MongoDb:Password"),
            AuthenticationSource = authSource,
        };
        return MongoClientSettings.FromUrl(url.ToMongoUrl());
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
