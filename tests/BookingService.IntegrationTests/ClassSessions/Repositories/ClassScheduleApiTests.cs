using System.Net.Http.Json;
using BookingService.Api;
using BookingService.Domain;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BookingService.IntegrationTests.ClassSessions.Repositories;

/// <summary>Anyone sees the class schedule without logging in, with the names of the centres and class types (#254).</summary>
public sealed class ClassScheduleApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CenterId = Guid.NewGuid();
    private static readonly Guid ClassTypeId = Guid.NewGuid();
    private string _database = "";

    [SetUp]
    public async Task StoreTheReferenceDataAsync()
    {
        // Booking only reads centres and class types (#255 writes them), so the test stores them with the driver
        _database = $"booking-{Guid.NewGuid():N}";
        using var client = new MongoClient(MongoDbContainerSetUp.ConnectionString);
        IMongoDatabase database = client.GetDatabase(_database);
        await database.GetCollection<BsonDocument>("centers")
            .InsertOneAsync(new BsonDocument { { "_id", new BsonBinaryData(CenterId, GuidRepresentation.Standard) }, { "Name", "FitLife Center 1" } })
            .ConfigureAwait(false);
        await database.GetCollection<BsonDocument>("classTypes")
            .InsertOneAsync(new BsonDocument { { "_id", new BsonBinaryData(ClassTypeId, GuidRepresentation.Standard) }, { "Name", "Yoga" } })
            .ConfigureAwait(false);
    }

    [Test]
    public async Task GetClassSessions_WithoutAToken_ReturnsUpcomingClassSessionsOrderedByStart()
    {
        // Arrange
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, _database, new FakeTimeProvider(Now));
        ClassSession later = ClassSessionAt(CenterId, Now.AddDays(2));
        ClassSession sooner = ClassSessionAt(CenterId, Now.AddHours(1));
        await factory.AddAsync(later).ConfigureAwait(false);
        await factory.AddAsync(ClassSessionAt(CenterId, Now.AddHours(-1))).ConfigureAwait(false);
        await factory.AddAsync(sooner).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        ClassSessionResponse[]? schedule = await client.GetFromJsonAsync<ClassSessionResponse[]>(new Uri("/class-sessions", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(schedule, Is.EqualTo(new[] { ResponseFor(sooner, "FitLife Center 1"), ResponseFor(later, "FitLife Center 1") }));
    }

    [Test]
    public async Task GetClassSessions_WithAClassSessionStartingNow_LeavesItOut()
    {
        // Arrange: "after now" is strict, so the class session that starts this instant is not upcoming
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, _database, new FakeTimeProvider(Now));
        await factory.AddAsync(ClassSessionAt(CenterId, Now)).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        ClassSessionResponse[]? schedule = await client.GetFromJsonAsync<ClassSessionResponse[]>(new Uri("/class-sessions", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(schedule, Is.Empty);
    }

    [Test]
    public async Task GetClassSessions_WithACenterId_ReturnsOnlyThatCentre()
    {
        // Arrange
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, _database, new FakeTimeProvider(Now));
        ClassSession here = ClassSessionAt(CenterId, Now.AddDays(1));
        await factory.AddAsync(here).ConfigureAwait(false);
        await factory.AddAsync(ClassSessionAt(Guid.NewGuid(), Now.AddDays(1))).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        ClassSessionResponse[]? schedule = await client.GetFromJsonAsync<ClassSessionResponse[]>(new Uri($"/class-sessions?centerId={CenterId}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(schedule, Is.EqualTo(new[] { ResponseFor(here, "FitLife Center 1") }));
    }

    [Test]
    public async Task GetClassSessions_WithNothingStored_ReturnsAnEmptyList()
    {
        // Arrange
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, _database, new FakeTimeProvider(Now));
        using HttpClient client = factory.CreateClient();

        // Act
        ClassSessionResponse[]? schedule = await client.GetFromJsonAsync<ClassSessionResponse[]>(new Uri("/class-sessions", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(schedule, Is.Empty);
    }

    private static ClassSession ClassSessionAt(Guid centerId, DateTimeOffset startTime)
    {
        return new ClassSession(Guid.NewGuid(), new Capacity(2), startTime, new ClassSessionDetails(centerId, ClassTypeId, new Instructor("Mette"), TimeSpan.FromMinutes(45)));
    }

    private static ClassSessionResponse ResponseFor(ClassSession classSession, string centerName)
    {
        return new ClassSessionResponse(classSession.Id, classSession.CenterId, centerName, ClassTypeId, "Yoga", "Mette", classSession.StartTime, DurationMinutes: 45, Capacity: 2, FreeSeats: 2, IsCancelled: false);
    }
}
