using BinVaBun.DiscordBot.Configuration;
using Microsoft.Extensions.Options;

namespace BinVaBun.DiscordBot.Services;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    TimeZoneInfo Zone { get; }
}

public sealed class SystemClock(IOptions<CompanionOptions> options) : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public TimeZoneInfo Zone { get; } = TimeZones.Resolve(options.Value.TimeZone);
}

public static class TimeZones
{
    /// <summary>Resolves an IANA or Windows zone id, falling back to a fixed UTC+7.</summary>
    public static TimeZoneInfo Resolve(string id)
    {
        foreach (var candidate in new[] { id, "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }
}

public static class ClockExtensions
{
    public static DateTimeOffset Now(this IClock clock) => TimeZoneInfo.ConvertTime(clock.UtcNow, clock.Zone);
    public static DateOnly Today(this IClock clock) => DateOnly.FromDateTime(clock.Now().DateTime);
}

