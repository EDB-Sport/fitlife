using MongoDB.Driver;
using NotificationService.Models;

namespace NotificationService.Repositories;

/// <summary>Notifications in the <c>notifications</c> collection, keyed by their id, so a second insert is refused.</summary>
/// <param name="database">The service's database.</param>
public sealed class MongoNotificationRepository(IMongoDatabase database) : INotificationRepository
{
    private readonly IMongoCollection<Notification> _notifications = database.GetCollection<Notification>("notifications");

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
}
