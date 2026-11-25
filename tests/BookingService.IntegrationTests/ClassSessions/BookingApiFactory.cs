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
    private readonly bool _everySaveIsStale;

    /// <param name="connectionString">The MongoDB server.</param>
    /// <param name="database">The database this service uses.</param>
    /// <param name="clock">The fake clock the service tells the time with.</param>
    /// <param name="seed">Loads the demo class schedule at start-up.</param>
    /// <param name="everySaveIsStale">
    /// Every save reports that another request saved the class session first, as two requests at the same moment would.
    /// </param>
    public BookingApiFactory(string connectionString, string database, FakeTimeProvider clock, bool seed = false, bool everySaveIsStale = false)
    {
        _connectionString = connectionString;
        _database = database;
        _seed = seed;
        _everySaveIsStale = everySaveIsStale;
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
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
            if (_everySaveIsStale)
            {
                services.Replace(ServiceDescriptor.Scoped<IClassSessionRepository>(provider =>
                    new StaleSaveRepository(ActivatorUtilities.CreateInstance<ClassSessionRepository>(provider))));
            }
        });
    }
}
