using System.Globalization;
using System.Security.Claims;
using MemoriesApp.Api.DTOs;
using MemoriesApp.Api.Hubs;
using MemoriesApp.Api.Models;
using MemoriesApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;

namespace MemoriesApp.Api.Controllers;

/// <summary>
/// Endpoints used by the BinVaBun Discord companion. They operate on the existing
/// <c>posts</c> collection and run as the user named in the X-Bot-User header.
/// Error messages returned here are curated and safe to show in Discord.
/// </summary>
[ApiController]
[Route("api/bot")]
[Authorize(AuthenticationSchemes = BotApiKeyAuthenticationHandler.SchemeName)]
public class BotController : ControllerBase
{
    private readonly MongoDbService _mongoDb;
    private readonly CloudinaryService _cloudinary;
    private readonly NotificationService _notificationService;
    private readonly SignalRService _signalRService;
    private readonly BotMemoryService _memories;
    private readonly IHubContext<MemoriesHub> _hubContext;
    private readonly ILogger<BotController> _logger;

    public BotController(
        MongoDbService mongoDb,
        CloudinaryService cloudinary,
        NotificationService notificationService,
        SignalRService signalRService,
        BotMemoryService memories,
        IHubContext<MemoriesHub> hubContext,
        ILogger<BotController> logger)
    {
        _mongoDb = mongoDb;
        _cloudinary = cloudinary;
        _notificationService = notificationService;
        _signalRService = signalRService;
        _memories = memories;
        _hubContext = hubContext;
        _logger = logger;
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

    private DateOnly ToLocalDate(DateTime utcOrLocal)
    {
        var utc = DateTime.SpecifyKind(utcOrLocal, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, _memories.Zone));
    }

    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id = UserId,
        username = User.FindFirst(ClaimTypes.Name)?.Value,
        displayName = User.FindFirst("DisplayName")?.Value,
        timeZone = _memories.Zone.Id,
        today = _memories.TodayLocal().ToString("yyyy-MM-dd")
    });

    [HttpGet("memories")]
    public async Task<ActionResult<BotMemoryPageDto>> List(
        [FromQuery] string? query = null,
        [FromQuery] string? tag = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] bool hasImage = false,
        [FromQuery] string order = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var filtered = (await _memories.LoadAllAsync(ct))
            .Where(p => _memories.Matches(p, query, tag))
            .Select(p => (Post: p, Date: _memories.EffectiveDate(p)))
            .Where(x => !year.HasValue || x.Date.Year == year.Value)
            .Where(x => !month.HasValue || x.Date.Month == month.Value)
            .Where(x => !hasImage || (x.Post.Images?.Count ?? 0) > 0);

        var ordered = string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase)
            ? filtered.OrderBy(x => x.Date).ThenBy(x => x.Post.CreatedAt)
            : filtered.OrderByDescending(x => x.Date).ThenByDescending(x => x.Post.CreatedAt);

        var all = ordered.ToList();
        return Ok(new BotMemoryPageDto
        {
            Items = all.Skip((page - 1) * pageSize).Take(pageSize).Select(x => _memories.ToDto(x.Post)).ToList(),
            TotalCount = all.Count,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(all.Count / (double)pageSize)
        });
    }

    [HttpGet("memories/random")]
    public async Task<ActionResult<BotMemoryDto>> Random(
        [FromQuery] bool hasImage = false,
        [FromQuery] string? exclude = null,
        CancellationToken ct = default)
    {
        var excluded = (exclude ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        var pool = (await _memories.LoadAllAsync(ct))
            .Where(p => !hasImage || (p.Images?.Count ?? 0) > 0)
            .ToList();
        // Prefer memories not recently surfaced, but fall back to the whole pool.
        var preferred = pool.Where(p => !excluded.Contains(p.Id)).ToList();
        var pick = preferred.Count > 0 ? preferred : pool;
        if (pick.Count == 0) return NotFound(new { error = "There are no memories here yet." });
        return Ok(_memories.ToDto(pick[System.Security.Cryptography.RandomNumberGenerator.GetInt32(pick.Count)]));
    }

    /// <summary>Memories from earlier years that fall on the given month/day (default: today).</summary>
    [HttpGet("memories/on-this-day")]
    public async Task<ActionResult<List<BotMemoryDto>>> OnThisDay(
        [FromQuery] int? month = null, [FromQuery] int? day = null, CancellationToken ct = default)
    {
        var today = _memories.TodayLocal();
        var m = month ?? today.Month;
        var d = day ?? today.Day;
        var items = (await _memories.LoadAllAsync(ct))
            .Select(p => (Post: p, Date: _memories.EffectiveDate(p)))
            .Where(x => x.Date.Month == m && x.Date.Day == d && x.Date.Year < today.Year)
            .OrderByDescending(x => x.Date)
            .Select(x => _memories.ToDto(x.Post))
            .ToList();
        return Ok(items);
    }

    [HttpGet("memories/{id}")]
    public async Task<ActionResult<BotMemoryDto>> Get(string id, CancellationToken ct)
    {
        var post = await _memories.GetAsync(id, ct);
        if (post == null) return NotFound(new { error = "I couldn't find that memory." });
        return Ok(_memories.ToDto(post));
    }

    [HttpPost("memories")]
    public async Task<ActionResult<BotMemoryDto>> Create([FromForm] CreateBotMemoryDto dto, CancellationToken ct)
    {
        var title = dto.Title?.Trim();
        var content = dto.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(content))
            return BadRequest(new { error = "A memory needs at least a title or a description." });
        if (title is { Length: > 100 }) return BadRequest(new { error = "The title is too long (100 characters max)." });
        if (content.Length > 4000) return BadRequest(new { error = "The description is too long (4000 characters max)." });

        var tags = (dto.Tags ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(BotMemoryService.NormalizeTag)
            .Where(t => t.Length > 0 && t.Length <= 30)
            .Distinct()
            .Take(10)
            .ToList();

        var today = _memories.TodayLocal();
        var date = today;
        if (!string.IsNullOrWhiteSpace(dto.Date))
        {
            if (!DateOnly.TryParseExact(dto.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return BadRequest(new { error = "I couldn't read that date." });
            if (date > today.AddDays(1))
                return BadRequest(new { error = "That date hasn't happened yet." });
        }

        var images = new List<string>();
        if (dto.Image is { Length: > 0 })
        {
            if (dto.Image.ContentType == null || !dto.Image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "That file doesn't look like an image." });
            try
            {
                images.Add(await _cloudinary.UploadImageAsync(dto.Image));
            }
            catch (ArgumentException ex)
            {
                // CloudinaryService validation messages are safe and descriptive.
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Photo upload failed for bot memory");
                return StatusCode(502, new { error = "The photo couldn't be stored right now." });
            }
        }

        var userId = UserId;
        var user = await _mongoDb.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(ct);
        var authorName = user?.DisplayName ?? User.FindFirst("DisplayName")?.Value ?? "Someone";

        var post = new MemoryPost
        {
            Title = string.IsNullOrEmpty(title) ? null : title,
            // The web app only renders Content, so fall back to the title when there is no description.
            Content = string.IsNullOrEmpty(content) ? title! : content,
            Images = images,
            Tags = tags,
            MemoryDate = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            AuthorId = userId,
            AuthorName = authorName,
            AuthorAvatar = user?.Avatar,
            CreatedAt = DateTime.UtcNow
        };
        await _mongoDb.Posts.InsertOneAsync(post, cancellationToken: ct);

        await NotifyWebAppAsync(post, userId);

        return Ok(_memories.ToDto(post));
    }

    /// <summary>Mirror what PostsController does so the web app stays in sync (best effort).</summary>
    private async Task NotifyWebAppAsync(MemoryPost post, string userId)
    {
        try
        {
            var others = await _mongoDb.Users.Find(u => u.Id != userId).ToListAsync();
            foreach (var other in others)
            {
                var message = $"{post.AuthorName} đã đăng một bài viết mới";
                var created = await _notificationService.CreateNotificationAsync(other.Id, "post", message, post.Id, userId, post.AuthorName);
                await _signalRService.BroadcastNewNotification(other.Id, new NotificationDto
                {
                    Id = created.Id,
                    Type = "post",
                    Message = message,
                    PostId = post.Id,
                    FromUserId = userId,
                    FromUserName = post.AuthorName,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _signalRService.BroadcastNewPost(new PostDto
            {
                Id = post.Id,
                Content = post.Content,
                Images = post.Images,
                AuthorId = post.AuthorId,
                AuthorName = post.AuthorName,
                AuthorAvatar = post.AuthorAvatar,
                CreatedAt = post.CreatedAt,
                LikesCount = 0,
                IsLiked = false,
                IsOwner = false,
                Comments = new List<CommentDto>()
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bot memory saved but web notification/broadcast failed");
        }
    }

    [HttpGet("dating")]
    public async Task<IActionResult> GetDatingStats([FromQuery] int? year = null, [FromQuery] int? month = null, CancellationToken ct = default)
    {
        var allDates = await _mongoDb.MarkedDates.Find(_ => true).ToListAsync(ct);
        var today = _memories.TodayLocal();

        var localDates = allDates
            .Select(d => new { MarkedDate = d, LocalDate = ToLocalDate(d.Date) })
            .OrderByDescending(x => x.LocalDate)
            .ToList();

        var totalCount = localDates.Count;
        var thisYearCount = localDates.Count(x => x.LocalDate.Year == today.Year);
        var thisMonthCount = localDates.Count(x => x.LocalDate.Year == today.Year && x.LocalDate.Month == today.Month);
        var isMarkedToday = localDates.Any(x => x.LocalDate == today);

        var latest = localDates.FirstOrDefault();
        string? latestDateStr = latest?.LocalDate.ToString("yyyy-MM-dd");
        int? daysSinceLast = latest != null ? today.DayNumber - latest.LocalDate.DayNumber : null;

        var recentDates = localDates.Take(15).Select(x => x.LocalDate.ToString("yyyy-MM-dd")).Distinct().ToList();

        var qYear = year ?? today.Year;
        var qMonth = month ?? today.Month;
        var datesInMonth = localDates
            .Where(x => x.LocalDate.Year == qYear && x.LocalDate.Month == qMonth)
            .Select(x => x.LocalDate.ToString("yyyy-MM-dd"))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        return Ok(new
        {
            totalCount,
            thisYearCount,
            thisMonthCount,
            isMarkedToday,
            today = today.ToString("yyyy-MM-dd"),
            latestDate = latestDateStr,
            daysSinceLastDate = daysSinceLast,
            recentDates,
            datesInMonth,
            year = qYear,
            month = qMonth
        });
    }

    public class MarkDatingDateRequest
    {
        public string? Date { get; set; }
    }

    [HttpPost("dating/mark")]
    public async Task<IActionResult> MarkDatingDate([FromBody] MarkDatingDateRequest? dto, CancellationToken ct = default)
    {
        var today = _memories.TodayLocal();
        var targetDate = today;

        if (!string.IsNullOrWhiteSpace(dto?.Date))
        {
            if (!DateOnly.TryParseExact(dto.Date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out targetDate))
            {
                return BadRequest(new { error = "I couldn't read that date. Use yyyy-MM-dd." });
            }
        }

        var allDates = await _mongoDb.MarkedDates.Find(_ => true).ToListAsync(ct);
        var existing = allDates.FirstOrDefault(d => ToLocalDate(d.Date) == targetDate);

        if (existing != null)
        {
            return Ok(new
            {
                action = "already_exists",
                date = targetDate.ToString("yyyy-MM-dd"),
                totalCount = allDates.Count,
                message = "Hôm nay hai đứa mình đã ghi nhận ngày hẹn hò rồi nè 💕"
            });
        }

        var dtUtc = DateTime.SpecifyKind(targetDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var newMarkedDate = new MarkedDate
        {
            Date = dtUtc,
            CreatedBy = UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _mongoDb.MarkedDates.InsertOneAsync(newMarkedDate, cancellationToken: ct);

        try
        {
            await _hubContext.Clients.All.SendAsync("DateMarked", newMarkedDate, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR DateMarked broadcast failed");
        }

        return Ok(new
        {
            action = "marked",
            date = targetDate.ToString("yyyy-MM-dd"),
            totalCount = allDates.Count + 1,
            message = "Đã cộng thêm ngày hẹn hò thành công! 💕"
        });
    }

    [HttpDelete("dating/{date}")]
    public async Task<IActionResult> UnmarkDatingDate(string date, CancellationToken ct = default)
    {
        if (!DateOnly.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var targetDate))
        {
            return BadRequest(new { error = "I couldn't read that date. Use yyyy-MM-dd." });
        }

        var allDates = await _mongoDb.MarkedDates.Find(_ => true).ToListAsync(ct);
        var existing = allDates.FirstOrDefault(d => ToLocalDate(d.Date) == targetDate);

        if (existing == null)
        {
            return NotFound(new { error = "Không tìm thấy ngày này trong lịch hẹn hò." });
        }

        await _mongoDb.MarkedDates.DeleteOneAsync(d => d.Id == existing.Id, cancellationToken: ct);

        try
        {
            await _hubContext.Clients.All.SendAsync("DateUnmarked", existing.Id, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR DateUnmarked broadcast failed");
        }

        return Ok(new
        {
            action = "unmarked",
            date = targetDate.ToString("yyyy-MM-dd"),
            totalCount = allDates.Count - 1,
            message = "Đã bỏ đánh dấu ngày hẹn hò."
        });
    }
}

