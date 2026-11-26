using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BookingService.IntegrationTests.ClassSessions.Repositories;

/// <summary>
/// The service finds MongoDB from MongoDb:Host, Port, Database, Username and Password (from Vault, #17), as IdentityService does,
/// and the login lives in the service's own database unless MongoDb:AuthSource names another one (#253).
/// </summary>
public sealed class MongoDbSettingsTests
{
    [Test]
    public async Task GetClassSessions_WithTheLoginInItsOwnSettings_Returns200()
    {
        // Arrange
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider());
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/class-sessions", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetClassSessions_WithoutAuthSource_LogsInToTheServiceDatabase()
    {
        // Arrange: a user that lives in the service's database, as deploy/compose/mongodb/init creates booking-svc
        string database = $"booking-{Guid.NewGuid():N}";
        string password = Guid.NewGuid().ToString("N");
        using var admin = new MongoClient(MongoDbContainerSetUp.ConnectionString);
        await admin.GetDatabase(database).RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "createUser", "booking-svc" },
            { "pwd", password },
            { "roles", new BsonArray { new BsonDocument { { "role", "readWrite" }, { "db", database } } } },
        }).ConfigureAwait(false);
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, database, new FakeTimeProvider());
        using WebApplicationFactory<Program> asServiceUser = factory.WithWebHostBuilder(host => host
            .UseSetting("MongoDb:Username", "booking-svc")
            .UseSetting("MongoDb:Password", password)
            .UseSetting("MongoDb:AuthSource", ""));
        using HttpClient client = asServiceUser.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/class-sessions", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
