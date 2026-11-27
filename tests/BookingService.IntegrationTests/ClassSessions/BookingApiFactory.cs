using System.Globalization;
using System.Net.Http.Headers;
using BookingService.Application;
using BookingService.Domain;
using BookingService.Repositories;
using BookingService.Seed;
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

    /// <param name="connectionString">The MongoDB server, with the login in it. The factory gives the service its parts as MongoDb:* settings.</param>
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

    /// <summary>Waits until the demo data is loaded: the seed runs after start-up, in the background (#255).</summary>
    public Task SeedFinishedAsync()
    {
        return Services.GetRequiredService<ClassSessionSeedStatus>().Finished;
    }

    /// <summary>Gets what Membership answers; a client from <c>CreateClient()</c> carries a Medlem token (#260).</summary>
    public FakeMembershipClient Membership { get; } = new();

    /// <summary>Stores a class session through the service's own repository.</summary>
    public async Task AddAsync(ClassSession classSession)
    {
        using IServiceScope scope = Services.CreateScope();
        IClassSessionRepository repository = scope.ServiceProvider.GetRequiredService<IClassSessionRepository>();
        await repository.AddAsync(classSession, CancellationToken.None).ConfigureAwait(false);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The container's login lives in admin, so AuthSource says so; compose's booking-svc login lives in booking (the default)
        var server = new Uri(_connectionString);
        string[] login = server.UserInfo.Split(':');
        builder.UseSetting("MongoDb:Host", server.Host);
        builder.UseSetting("MongoDb:Port", server.Port.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("MongoDb:Username", Uri.UnescapeDataString(login[0]));
        builder.UseSetting("MongoDb:Password", Uri.UnescapeDataString(login[1]));
        builder.UseSetting("MongoDb:AuthSource", "admin");
        builder.UseSetting("MongoDb:Database", _database);
        builder.UseSetting("Seed:Enabled", _seed.ToString());
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
            services.Replace(ServiceDescriptor.Singleton<IMembershipClient>(Membership));
            if (_everySaveIsStale)
            {
                services.Replace(ServiceDescriptor.Scoped<IClassSessionRepository>(provider =>
                    new StaleSaveRepository(ActivatorUtilities.CreateInstance<ClassSessionRepository>(provider))));
            }
        });
    }

    /// <summary>Every client logs in as a member, as the member portal does; a test without a token clears the header.</summary>
    protected override void ConfigureClient(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(Guid.NewGuid()));
    }
}
