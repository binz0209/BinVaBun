using System.Globalization;
using System.Text;
using MemoriesApp.Api.DTOs;
using MemoriesApp.Api.Models;
using MongoDB.Driver;

namespace MemoriesApp.Api.Services;

/// <summary>
/// Read-side helpers for the bot's memory endpoints. Memories are the existing
/// <see cref="MemoryPost"/> documents; nothing is copied anywhere else.
/// Filtering happens in memory (comments/likes are excluded by projection), which is
/// appropriate for a two-person archive and gives diacritic-insensitive search.
/// </summary>
public class BotMemoryService
{
    private readonly MongoDbService _mongoDb;
    public TimeZoneInfo Zone { get; }

    public BotMemoryService(MongoDbService mongoDb, IConfiguration configuration)
    {
        _mongoDb = mongoDb;
        Zone = ResolveZone(configuration["Bot:TimeZone"] ?? "Asia/Ho_Chi_Minh");
    }

    public static TimeZoneInfo ResolveZone(string id)
    {
        foreach (var candidate in new[] { id, "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); } catch (TimeZoneNotFoundException) { } catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }

    public DateOnly TodayLocal() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));

    public DateOnly EffectiveDate(MemoryPost p)
    {
        if (p.MemoryDate.HasValue) return DateOnly.FromDateTime(p.MemoryDate.Value);
        var utc = DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Zone));
    }

    public async Task<List<MemoryPost>> LoadAllAsync(CancellationToken ct = default)
    {
        var projection = Builders<MemoryPost>.Projection
            .Exclude(p => p.Comments)
            .Exclude(p => p.Likes);
        return await _mongoDb.Posts.Find(Builders<MemoryPost>.Filter.Empty)
            .Project<MemoryPost>(projection)
            .ToListAsync(ct);
    }

    public async Task<MemoryPost?> GetAsync(string id, CancellationToken ct = default)
    {
        var projection = Builders<MemoryPost>.Projection.Exclude(p => p.Comments).Exclude(p => p.Likes);
        return await _mongoDb.Posts.Find(p => p.Id == id).Project<MemoryPost>(projection).FirstOrDefaultAsync(ct);
    }

    public BotMemoryDto ToDto(MemoryPost p) => new()
    {
        Id = p.Id,
        Title = DeriveTitle(p),
        Content = p.Content ?? string.Empty,
        Tags = p.Tags ?? new List<string>(),
        Images = p.Images ?? new List<string>(),
        AuthorName = p.AuthorName,
        CreatedAt = DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc),
        Date = EffectiveDate(p).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    };

    public static string DeriveTitle(MemoryPost p)
    {
        if (!string.IsNullOrWhiteSpace(p.Title)) return p.Title!;
        var firstLine = (p.Content ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(firstLine)) return "Untitled memory";
        return firstLine.Length <= 60 ? firstLine : firstLine[..57] + "...";
    }

    /// <summary>Lowercase, strip diacritics (including Vietnamese đ).</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c == 'đ' || c == 'Đ' ? 'd' : char.ToLowerInvariant(c));
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string NormalizeTag(string tag) => Normalize(tag.Trim().TrimStart('#')).Replace(' ', '-');

    public bool Matches(MemoryPost p, string? query, string? tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var wanted = NormalizeTag(tag);
            if (!(p.Tags ?? new()).Any(t => NormalizeTag(t) == wanted)) return false;
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            var haystack = Normalize($"{p.Title} {p.Content} {string.Join(' ', p.Tags ?? new())}");
            foreach (var token in Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!haystack.Contains(token)) return false;
            }
        }
        return true;
    }
}

