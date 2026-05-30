using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MemoriesApp.Api.Models;

public class Notification
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty; // "comment", "like", "post"

    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("postId")]
    public string? PostId { get; set; }

    [BsonElement("fromUserId")]
    public string? FromUserId { get; set; }

    [BsonElement("fromUserName")]
    public string? FromUserName { get; set; }

    [BsonElement("isRead")]
    public bool IsRead { get; set; } = false;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

