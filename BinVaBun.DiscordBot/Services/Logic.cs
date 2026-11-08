using System.Globalization;
using System.Text.RegularExpressions;
using BinVaBun.DiscordBot.Models;

namespace BinVaBun.DiscordBot.Services;

public static class AnniversaryCalculator
{
    /// <summary>
    /// The next date (on or after <paramref name="today"/>) the anniversary is observed,
    /// or null for a one-off date that has already passed. Feb 29 falls back to Feb 28 in common years.
    /// </summary>
    public static DateOnly? NextOccurrence(Anniversary a, DateOnly today)
    {
        if (!a.Yearly) return a.Date >= today ? a.Date : null;

        var candidate = InYear(a.Date, today.Year);
        if (candidate < today) candidate = InYear(a.Date, today.Year + 1);
        return candidate < a.Date ? a.Date : candidate;
    }

    public static DateOnly InYear(DateOnly source, int year)
    {
        var day = source.Month == 2 && source.Day == 29 && !DateTime.IsLeapYear(year) ? 28 : source.Day;
        return new DateOnly(year, source.Month, day);
    }

    public static int DaysUntil(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber;

    /// <summary>Completed years between the original date and the occurrence (the "Nth anniversary").</summary>
    public static int YearsAt(Anniversary a, DateOnly occurrence) => occurrence.Year - a.Date.Year;

    public static (int Years, int Months, int Days) Breakdown(DateOnly from, DateOnly to)
    {
        var years = to.Year - from.Year;
        var anchor = from.AddYears(years);
        if (anchor > to) { years--; anchor = from.AddYears(years); }
        var months = 0;
        while (anchor.AddMonths(months + 1) <= to) months++;
        var days = to.DayNumber - anchor.AddMonths(months).DayNumber;
        return (years, months, days);
    }
}

public static partial class FlexibleParser
{
    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "yyyy-M-d", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy"];

    private static readonly string[] DateTimeFormats =
        ["yyyy-MM-dd HH:mm", "yyyy-M-d H:mm", "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm", "dd-MM-yyyy HH:mm"];

    [GeneratedRegex(@"^(?:in|sau)?\s*(\d{1,4})\s*(m|min|mins|phut|phút|p|h|hr|hrs|gio|giờ|g|d|day|days|ngay|ngày|w|tuan|tuần)$", RegexOptions.IgnoreCase)]
    private static partial Regex RelativePattern();

    [GeneratedRegex(@"^(today|tomorrow|hom nay|hôm nay|ngay mai|ngày mai)?\s*(\d{1,2}):(\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex TimeOnlyPattern();

    public static bool TryParseDate(string? input, out DateOnly date) =>
        DateOnly.TryParseExact(input?.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>
    /// Understands "2026-12-24 19:30", "24/12/2026 19:30", "in 2h", "in 3 days", "sau 2h",
    /// "19:30" (next time that happens), "today 19:30", "hôm nay 19:30" and "tomorrow 09:00" in the given zone.
    /// </summary>
    public static bool TryParseDateTime(string? input, DateTimeOffset now, TimeZoneInfo zone, out DateTimeOffset result)
    {
        result = default;
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text)) return false;

        var rel = RelativePattern().Match(text);
        if (rel.Success)
        {
            var n = int.Parse(rel.Groups[1].Value, CultureInfo.InvariantCulture);
            var unit = rel.Groups[2].Value.ToLowerInvariant();
            result = unit switch
            {
                "m" or "min" or "mins" or "p" or "phut" or "phút" => now.AddMinutes(n),
                "h" or "hr" or "hrs" or "g" or "gio" or "giờ" => now.AddHours(n),
                "w" or "tuan" or "tuần" => now.AddDays(7 * n),
                _ => now.AddDays(n)
            };
            return true;
        }

        var tm = TimeOnlyPattern().Match(text);
        if (tm.Success && int.Parse(tm.Groups[2].Value) is >= 0 and <= 23 && int.Parse(tm.Groups[3].Value) is >= 0 and <= 59)
        {
            var time = new TimeOnly(int.Parse(tm.Groups[2].Value), int.Parse(tm.Groups[3].Value));
            var localToday = DateOnly.FromDateTime(now.DateTime);
            var word = tm.Groups[1].Value.ToLowerInvariant();
            var day = word is "tomorrow" or "ngay mai" or "ngày mai" ? localToday.AddDays(1) : localToday;
            var candidate = ToOffset(day, time, zone);
            if (word == "" && candidate <= now) candidate = ToOffset(day.AddDays(1), time, zone);
            result = candidate;
            return true;
        }

        if (DateTime.TryParseExact(text, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            result = ToOffset(DateOnly.FromDateTime(dt), TimeOnly.FromDateTime(dt), zone);
            return true;
        }
        return false;
    }

    public static DateTimeOffset ToOffset(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) local = local.AddHours(1);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    public static bool TryParseTimeOfDay(string? input, out TimeOnly time) =>
        TimeOnly.TryParseExact(input?.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}

public static class ScheduleMath
{
    /// <summary>
    /// True when <paramref name="now"/> is at/after the scheduled time on the same local day and
    /// within the catch-up window. Combined with a persisted "already sent" claim this gives
    /// exactly-once daily posts that survive restarts without firing at absurd hours.
    /// </summary>
    public static bool IsDue(DateTimeOffset localNow, TimeOnly scheduled, TimeSpan catchUp)
    {
        var elapsed = TimeOnly.FromDateTime(localNow.DateTime).ToTimeSpan() - scheduled.ToTimeSpan();
        return elapsed >= TimeSpan.Zero && elapsed <= catchUp;
    }
}

