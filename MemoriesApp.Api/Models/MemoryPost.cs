using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MemoriesApp.Api.Models;

public class MemoryPost
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("images")]
    public List<string> Images { get; set; } = new();

    [BsonElement("authorId")]
    public string AuthorId { get; set; } = string.Empty;

    [BsonElement("authorName")]
    public string AuthorName { get; set; } = string.Empty;

    [BsonElement("authorAvatar")]
    public string? AuthorAvatar { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [BsonElement("likes")]
    public List<string> Likes { get; set; } = new();

    [BsonElement("comments")]
    public List<Comment> Comments { get; set; } = new();
}

public class Comment
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("authorId")]
    public string AuthorId { get; set; } = string.Empty;

    [BsonElement("authorName")]
    public string AuthorName { get; set; } = string.Empty;

    [BsonElement("authorAvatar")]
    public string? AuthorAvatar { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

