using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MemoriesApp.Api.DTOs;
using MemoriesApp.Api.Services;
using System.Security.Claims;

namespace MemoriesApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationsController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetNotifications()
    {
        var userId = GetUserId();
        var notifications = await _notificationService.GetUserNotificationsAsync(userId);

        var dtos = notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            Type = n.Type,
            Message = n.Message,
            PostId = n.PostId,
            FromUserId = n.FromUserId,
            FromUserName = n.FromUserName,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("{id}/read")]
    public async Task<ActionResult> MarkAsRead(string id)
    {
        await _notificationService.MarkAsReadAsync(id);
        return Ok();
    }

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return Ok();
    }
}

