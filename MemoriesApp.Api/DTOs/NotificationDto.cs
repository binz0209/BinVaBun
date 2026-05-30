namespace MemoriesApp.Api.DTOs;

public class NotificationDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? PostId { get; set; }
    public string? FromUserId { get; set; }
    public string? FromUserName { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

