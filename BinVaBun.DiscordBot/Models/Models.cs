using Discord.Interactions;

namespace BinVaBun.DiscordBot.Models;

/// <summary>A MemoriesApp memory as returned by /api/bot/memories.</summary>
public sealed class Memory
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> Images { get; set; } = new();
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    /// <summary>yyyy-MM-dd</summary>
    public string Date { get; set; } = string.Empty;

    public DateOnly DateValue => DateOnly.TryParse(Date, out var d) ? d : DateOnly.FromDateTime(CreatedAt);
    public string? FirstImage => Images.FirstOrDefault();
    /// <summary>True when the description just repeats the title (so it need not be shown twice).</summary>
    public bool ContentIsTitle => string.IsNullOrWhiteSpace(Content) || Content.Trim() == Title;
}

public sealed class MemoryPage
{
    public List<Memory> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public sealed record MemoryQuery(
    string? Query = null, string? Tag = null, int? Year = null, int? Month = null,
    bool HasImage = false, bool Ascending = false, int Page = 1, int PageSize = 5);

public sealed record CreateMemoryRequest(
    string Title, string? Description, DateOnly? Date, string? Tags, ImageUpload? Image);

public sealed record ImageUpload(byte[] Bytes, string FileName, string ContentType);

public sealed class ApiIdentity
{
    public string Id { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? TimeZone { get; set; }
}

public sealed class DatingStats
{
    public int TotalCount { get; set; }
    public int ThisYearCount { get; set; }
    public int ThisMonthCount { get; set; }
    public bool IsMarkedToday { get; set; }
    public string Today { get; set; } = string.Empty;
    public string? LatestDate { get; set; }
    public int? DaysSinceLastDate { get; set; }
    public List<string> RecentDates { get; set; } = new();
    public List<string> DatesInMonth { get; set; } = new();
    public int Year { get; set; }
    public int Month { get; set; }
}

public sealed class MarkDateResult
{
    public string Action { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

// ── Records persisted through the API's key/value store ──

public enum AnniversaryKind
{
    [ChoiceDisplay("Ngày yêu")] Relationship,
    [ChoiceDisplay("Lần đầu gặp gỡ")] FirstMeeting,
    [ChoiceDisplay("Buổi hẹn đầu tiên")] FirstDate,
    [ChoiceDisplay("Sinh nhật")] Birthday,
    [ChoiceDisplay("Chuyến đi / Du lịch")] Trip,
    [ChoiceDisplay("Khác")] Other
}

public sealed class Anniversary
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public AnniversaryKind Kind { get; set; }
    public bool Yearly { get; set; } = true;
    public ulong CreatedBy { get; set; }
}

public sealed class PersonalReminder
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset DueAt { get; set; }
    /// <summary>True = mention both of us, false = only the creator.</summary>
    public bool ForBoth { get; set; } = true;
    public ulong CreatedBy { get; set; }
}

public sealed class LoveNote
{
    public string Id { get; set; } = string.Empty;
    public ulong From { get; set; }
    public ulong To { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset DeliverAt { get; set; }
    public bool Delivered { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class DailyAnswer
{
    public string Date { get; set; } = string.Empty;
    public ulong DiscordId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public DateTimeOffset AnsweredAt { get; set; }
}

public enum QuizType { Date, Title }

public sealed class QuizOption
{
    public string MemoryId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class QuizRound
{
    public string Id { get; set; } = string.Empty;
    public string MemoryId { get; set; } = string.Empty;
    public QuizType Type { get; set; }
    public List<QuizOption> Options { get; set; } = new();
    public ulong CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class QuizAnswer
{
    public string RoundId { get; set; } = string.Empty;
    public ulong DiscordId { get; set; }
    public string Guess { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool Exact { get; set; }
}

public sealed class ScheduleSettings
{
    public ulong? ChannelId { get; set; }
    public string? DailyMemoryTime { get; set; }
    public string? DailyQuestionTime { get; set; }
    public string? AnniversaryTime { get; set; }
    public bool DailyMemoryEnabled { get; set; } = true;
    public bool DailyQuestionEnabled { get; set; } = true;
}

/// <summary>Payload stored with a sent-ledger claim.</summary>
public sealed class SentMarker
{
    public string? MemoryId { get; set; }
    public DateTimeOffset At { get; set; }
}

public static class StoreKinds
{
    public const string Anniversary = "anniversary";
    public const string Reminder = "reminder";
    public const string LoveNote = "lovenote";
    public const string DailyAnswer = "dq-answer";
    public const string DailyReveal = "dq-reveal";
    public const string QuizRound = "quiz-round";
    public const string QuizAnswer = "quiz-answer";
    public const string QuizReveal = "quiz-reveal";
    public const string Settings = "settings";
    public const string Sent = "sent";
}
