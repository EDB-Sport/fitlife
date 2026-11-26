using System.Net.Http.Json;
using BookingService.Api;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BookingService.IntegrationTests.ClassSessions.Repositories;

/// <summary>
/// A class session stored before #252 has no InstructorName and no Duration. Booking still starts and lists it, with the
/// defaults CONTEXT.md names (#396).
/// </summary>
public sealed class ClassSessionStoredBeforeInstructorsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Start_WithAClassSessionStoredBeforeInstructors_StartsAndListsIt()
    {
        // Arrange: the document as Booking stored it before #252, written with the driver
        string database = $"booking-{Guid.NewGuid():N}";
        var id = Guid.NewGuid();
        using (var mongo = new MongoClient(MongoDbContainerSetUp.ConnectionString))
        {
            await mongo.GetDatabase(database).GetCollection<BsonDocument>("classSessions").InsertOneAsync(new BsonDocument
            {
                { "_id", new BsonBinaryData(id, GuidRepresentation.Standard) },
                { "CenterId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "ClassTypeId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "Capacity", 10 },
                { "StartTime", Now.AddDays(1).UtcDateTime },
                { "IsCancelled", false },
                { "Reservations", new BsonArray() },
                { "Waitlist", new BsonArray() },
                { "Version", 1 },
            }).ConfigureAwait(false);
        }

        // The seeder reads every class session at start, which is where the service stopped
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, database, new FakeTimeProvider(Now), seed: true);

        // Act
        using HttpClient client = factory.CreateClient();
        await factory.SeedFinishedAsync().ConfigureAwait(false);
        ClassSessionResponse[]? schedule = await client.GetFromJsonAsync<ClassSessionResponse[]>(new Uri("/class-sessions", UriKind.Relative)).ConfigureAwait(false);

        // Assert: the seed tops the schedule up around it (#255), so the old class session is one of several
        Assert.That(schedule?.Select(classSession => classSession.Id), Does.Contain(id));
    }
}
