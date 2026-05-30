using Microsoft.AspNetCore.SignalR;
using MemoriesApp.Api.DTOs;
using MemoriesApp.Api.Hubs;

namespace MemoriesApp.Api.Services;

public class SignalRService
{
    private readonly IHubContext<MemoriesHub> _hubContext;

    public SignalRService(IHubContext<MemoriesHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastNewPost(PostDto post)
    {
        await _hubContext.Clients.All.SendAsync("NewPost", post);
    }

    public async Task BroadcastUpdatedPost(PostDto post)
    {
        await _hubContext.Clients.All.SendAsync("PostUpdated", post);
    }

    public async Task BroadcastDeletedPost(string postId)
    {
        await _hubContext.Clients.All.SendAsync("PostDeleted", postId);
    }

    public async Task BroadcastNewComment(string postId, CommentDto comment)
    {
        await _hubContext.Clients.All.SendAsync("NewComment", postId, comment);
    }

    public async Task BroadcastPostLiked(string postId, bool isLiked, int likesCount, string userId)
    {
        await _hubContext.Clients.All.SendAsync("PostLiked", postId, isLiked, likesCount, userId);
    }

    public async Task BroadcastNewNotification(string userId, NotificationDto notification)
    {
        await _hubContext.Clients.User(userId).SendAsync("NewNotification", notification);
    }
}

