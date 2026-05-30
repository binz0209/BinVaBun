using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MemoriesApp.Api.Models;
using MemoriesApp.Api.Services;
using Microsoft.AspNetCore.SignalR;
using MemoriesApp.Api.Hubs;

namespace MemoriesApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CalendarController : ControllerBase
{
    private readonly CalendarService _calendarService;
    private readonly IHubContext<MemoriesHub> _hubContext;

    public CalendarController(CalendarService calendarService, IHubContext<MemoriesHub> hubContext)
    {
        _calendarService = calendarService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetMarkedDates()
    {
        var dates = await _calendarService.GetMarkedDatesAsync();
        return Ok(dates);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleMarkedDate([FromBody] MarkedDateRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "Không tìm thấy user id trong token" });
        }

        var existingDate = await _calendarService.GetMarkedDateAsync(request.Date);
        if (existingDate != null)
        {
            // If it exists, unmark it
            await _calendarService.DeleteMarkedDateAsync(existingDate.Id);
            await _hubContext.Clients.All.SendAsync("DateUnmarked", existingDate.Id);
            return Ok(new { action = "unmarked", id = existingDate.Id });
        }
        else
        {
            // If it doesn't exist, mark it
            var newMarkedDate = await _calendarService.CreateMarkedDateAsync(request.Date, userId);
            await _hubContext.Clients.All.SendAsync("DateMarked", newMarkedDate);
            return Ok(new { action = "marked", data = newMarkedDate });
        }
    }

    [HttpPost("batch")]
    public async Task<IActionResult> BatchUpdateDates([FromBody] BatchMarkedDateRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "Không tìm thấy user id trong token" });
        }

        var addedDates = request.AddedDates ?? new List<DateTime>();
        var removedDateIds = request.RemovedDateIds ?? new List<string>();

        var newDates = await _calendarService.BatchUpdateDatesAsync(addedDates, removedDateIds, userId);

        // Broadcast changes
        if (removedDateIds.Any() || newDates.Any())
        {
            await _hubContext.Clients.All.SendAsync("DatesBatchUpdated", new 
            { 
                added = newDates, 
                removedIds = removedDateIds 
            });
        }

        return Ok(new { added = newDates, removedIds = removedDateIds });
    }
}

public class MarkedDateRequest
{
    public DateTime Date { get; set; }
}

public class BatchMarkedDateRequest
{
    public List<DateTime> AddedDates { get; set; } = new();
    public List<string> RemovedDateIds { get; set; } = new();
}
