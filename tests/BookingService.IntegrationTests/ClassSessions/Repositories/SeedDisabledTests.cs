using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;

namespace BookingService.IntegrationTests.ClassSessions.Repositories;

/// <summary>Without <c>Seed:Enabled</c>, Booking writes no demo data (#255).</summary>
public sealed class SeedDisabledTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Start_WithoutSeedEnabled_WritesNothing()
    {
        // Arrange
        string database = $"booking-{Guid.NewGuid():N}";
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, database, new FakeTimeProvider(Now), seed: false);

        // Act: the first request starts the host
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage health = await client.GetAsync(new Uri("/health", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        using var mongo = new MongoClient(MongoDbContainerSetUp.ConnectionString);
        using IAsyncCursor<string> names = await mongo.GetDatabase(database).ListCollectionNamesAsync().ConfigureAwait(false);
        Assert.That(await names.ToListAsync().ConfigureAwait(false), Is.Empty);
    }
}
