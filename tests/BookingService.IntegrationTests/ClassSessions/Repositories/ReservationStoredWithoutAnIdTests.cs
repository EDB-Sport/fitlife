using BookingService.Application;
using BookingService.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BookingService.IntegrationTests.ClassSessions.Repositories;

/// <summary>A reservation stored before #192 has no id. It gets one when it is read, so ReservationMade can carry it.</summary>
public sealed class ReservationStoredWithoutAnIdTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Read_AReservationStoredWithoutAnId_GetsAnId()
    {
        // Arrange: a reservation as Booking stored it before #192, written with the driver
        string database = $"booking-{Guid.NewGuid():N}";
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, database, new FakeTimeProvider(Now));
        var id = Guid.NewGuid();
        using (var mongo = new MongoClient(MongoDbContainerSetUp.ConnectionString))
        {
            await mongo.GetDatabase(database).GetCollection<BsonDocument>("classSessions").InsertOneAsync(new BsonDocument
            {
                { "_id", new BsonBinaryData(id, GuidRepresentation.Standard) },
                { "CenterId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "ClassTypeId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "Capacity", 2 },
                { "StartTime", Now.AddDays(1).UtcDateTime },
                { "IsCancelled", false },
                {
                    "Reservations", new BsonArray
                    {
                        new BsonDocument { { "MemberId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) }, { "IsFromWaitlist", false } },
                    }
                },
                { "Waitlist", new BsonArray() },
                { "Version", 1 },
            }).ConfigureAwait(false);
        }

        // Act
        ClassSession stored = await ReadAsync(factory, id).ConfigureAwait(false);

        // Assert
        Assert.That(stored.Reservations.Single().Id, Is.Not.EqualTo(Guid.Empty));
    }

    private static async Task<ClassSession> ReadAsync(BookingApiFactory factory, Guid classSessionId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IClassSessionRepository>().GetByIdAsync(classSessionId, CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The class session wasn't stored.");
    }
}
