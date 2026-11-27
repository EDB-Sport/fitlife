using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using NotificationService.Models;

namespace NotificationService.Repositories;

/// <summary>Notifications in the <c>notifications</c> collection, keyed by their id, so a second insert is refused.</summary>
/// <param name="database">The service's database.</param>
public sealed class MongoNotificationRepository(IMongoDatabase database) : INotificationRepository
{
    private readonly IMongoCollection<Notification> _notifications = Collection(database);

    /// <inheritdoc />
    public async Task<bool> TryAddAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.InsertOneAsync(notification, options: null, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Notification>> ListForMemberAsync(string memberId, CancellationToken cancellationToken)
    {
        return await _notifications.Find(notification => notification.MemberId == memberId)
            .SortByDescending(notification => notification.OccurredAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IMongoCollection<Notification> Collection(IMongoDatabase database)
    {
        // The time as a BSON date (UTC), so MongoDB sorts by it; the driver's default for DateTimeOffset is an array
        BsonClassMap.TryRegisterClassMap<Notification>(map =>
        {
            map.AutoMap();
            map.MapMember(notification => notification.OccurredAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
        });
        return database.GetCollection<Notification>("notifications");
    }
}
