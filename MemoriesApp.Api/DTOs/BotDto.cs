using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace MemoriesApp.Api.DTOs;

public class BotMemoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> Images { get; set; } = new();
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    /// <summary>Effective calendar date (yyyy-MM-dd): memoryDate if set, else createdAt in the configured time zone.</summary>
    public string Date { get; set; } = string.Empty;
}

public class BotMemoryPageDto
{
    public List<BotMemoryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class CreateBotMemoryDto
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    /// <summary>Comma-separated tags.</summary>
    public string? Tags { get; set; }
    /// <summary>yyyy-MM-dd; defaults to today in the configured time zone.</summary>
    public string? Date { get; set; }
    public IFormFile? Image { get; set; }
}

public class BotRecordDto
{
    public string Kind { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

