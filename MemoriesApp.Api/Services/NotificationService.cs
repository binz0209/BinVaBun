using MemoriesApp.Api.Models;
using MongoDB.Driver;

namespace MemoriesApp.Api.Services;

public class NotificationService
{
    private readonly MongoDbService _mongoDb;

    public NotificationService(MongoDbService mongoDb)
    {
        _mongoDb = mongoDb;
    }

    public async Task<Notification> CreateNotificationAsync(
        string userId,
        string type,
        string message,
        string? postId = null,
        string? fromUserId = null,
        string? fromUserName = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Message = message,
            PostId = postId,
            FromUserId = fromUserId,
            FromUserName = fromUserName,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _mongoDb.Notifications.InsertOneAsync(notification);
        return notification;
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(string userId)
    {
        var filter = Builders<Notification>.Filter.Eq(n => n.UserId, userId);
        return await _mongoDb.Notifications
            .Find(filter)
            .SortByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(string notificationId)
    {
        var filter = Builders<Notification>.Filter.Eq(n => n.Id, notificationId);
        var update = Builders<Notification>.Update.Set(n => n.IsRead, true);
        await _mongoDb.Notifications.UpdateOneAsync(filter, update);
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var filter = Builders<Notification>.Filter.And(
            Builders<Notification>.Filter.Eq(n => n.UserId, userId),
            Builders<Notification>.Filter.Eq(n => n.IsRead, false)
        );
        var update = Builders<Notification>.Update.Set(n => n.IsRead, true);
        await _mongoDb.Notifications.UpdateManyAsync(filter, update);
    }
}

