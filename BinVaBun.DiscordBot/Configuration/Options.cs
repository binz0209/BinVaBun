using System.ComponentModel.DataAnnotations;

namespace BinVaBun.DiscordBot.Configuration;

/// <summary>Bound from env vars such as <c>Discord__Token</c>.</summary>
public sealed class DiscordOptions
{
    public const string Section = "Discord";

    [Required] public string Token { get; set; } = string.Empty;

    /// <summary>The private server. Slash commands are registered to this guild only.</summary>
    [Range(1ul, ulong.MaxValue, ErrorMessage = "Discord__GuildId is required.")]
    public ulong GuildId { get; set; }

    /// <summary>Fallback channel for scheduled posts until /companion setup picks one.</summary>
    public ulong? DefaultChannelId { get; set; }
}

/// <summary>How the bot reaches MemoriesApp.Api (e.g. <c>Api__BaseUrl</c>, <c>Api__BotKey</c>).</summary>
public sealed class ApiOptions
{
    public const string Section = "Api";

    [Required, Url] public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Shared secret matching Bot:ApiKey / BOT_API_KEY on the API.</summary>
    [Required] public string BotKey { get; set; } = string.Empty;

    /// <summary>Per-attempt timeout. Generous because the API host can cold-start.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>One of the two people allowed to use the bot (<c>Owners__0__DiscordId</c>, ...).</summary>
public sealed class OwnerOptions
{
    public ulong DiscordId { get; set; }

    /// <summary>The MemoriesApp username this Discord user acts as.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Friendly name used in messages. Defaults to the username.</summary>
    public string? Name { get; set; }
}

public sealed class CompanionOptions
{
    public const string Section = "Companion";

    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";

    /// <summary>Defaults, overridable at runtime with /companion setup (persisted via the API).</summary>
    public string DailyMemoryTime { get; set; } = "20:00";
    public string DailyQuestionTime { get; set; } = "09:00";
    public string AnniversaryTime { get; set; } = "08:00";

    /// <summary>Days before an anniversary to remind, comma separated; 0 = on the day.</summary>
    public string AnniversaryReminderDays { get; set; } = "7,3,1,0";

    /// <summary>If the bot was down at the scheduled time it still posts, but only within this window.</summary>
    public int CatchUpWindowHours { get; set; } = 6;

    public int TickSeconds { get; set; } = 30;

    public int[] ParseReminderDays()
    {
        var days = AnniversaryReminderDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) && n is >= 0 and <= 365 ? n : (int?)null)
            .Where(n => n.HasValue).Select(n => n!.Value)
            .Distinct().OrderDescending().ToArray();
        return days.Length > 0 ? days : new[] { 7, 3, 1, 0 };
    }
}

