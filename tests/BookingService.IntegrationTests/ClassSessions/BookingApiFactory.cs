using BookingService.Domain;
using BookingService.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>
/// Starts BookingService against one MongoDB database, with a fake clock. Two factories on the same database are the same
/// service before and after a restart.
/// </summary>
internal sealed class BookingApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _database;
    private readonly bool _seed;

    public BookingApiFactory(string connectionString, string database, FakeTimeProvider clock, bool seed = false)
    {
        _connectionString = connectionString;
        _database = database;
        _seed = seed;
        Clock = clock;
    }

    public FakeTimeProvider Clock { get; }

    /// <summary>Stores a class session through the service's own repository.</summary>
    public async Task AddAsync(ClassSession classSession)
    {
        using IServiceScope scope = Services.CreateScope();
        IClassSessionRepository repository = scope.ServiceProvider.GetRequiredService<IClassSessionRepository>();
        await repository.AddAsync(classSession, CancellationToken.None).ConfigureAwait(false);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("MongoDb:ConnectionString", _connectionString);
        builder.UseSetting("MongoDb:Database", _database);
        builder.UseSetting("Seed:Enabled", _seed.ToString());
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock)));
    }
}
