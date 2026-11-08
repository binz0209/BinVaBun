using System.Globalization;
using System.Text;
using BinVaBun.DiscordBot.Content;
using BinVaBun.DiscordBot.Hosting;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;
using Discord.Interactions;

namespace BinVaBun.DiscordBot.Modules;

[Group("dating", "Theo dõi những ngày hẹn hò và cột mốc ngọt ngào của hai đứa")]
[RequireCouple]
[CommandContextType(InteractionContextType.Guild)]
public sealed class DatingModule : CompanionModuleBase
{
    [SlashCommand("today", "Đánh dấu hôm nay (giờ VN) là ngày đi hẹn hò")]
    public async Task TodayAsync()
    {
        await DeferAsync();
        var today = Clock.Today();
        var result = await Api.MarkDatingDateAsync(Me.Username, today.ToString("yyyy-MM-dd"), Ct);

        if (result.Action == "already_exists")
        {
            var embed = Embeds.Base(Palette.Cream)
                .WithTitle("✨ Hôm nay đã ghi nhận rồi nè")
                .WithDescription($"Ngày hôm nay (**{FormatVnDate(today)}**) đã có trong lịch hẹn hò của hai đứa rồi! 💕\n\nTổng cộng hai đứa mình đã có **{result.TotalCount}** ngày đi iu cùng nhau.")
                .WithFooter("Dùng /dating calendar hoặc /dating detail để xem lịch chi tiết")
                .Build();
            await FollowupAsync(embed: embed);
            return;
        }

        var successEmbed = Embeds.Base(Palette.Pink)
            .WithTitle("💕 Đã cộng thêm ngày hẹn hò!")
            .WithDescription($"Hôm nay (**{FormatVnDate(today)}**) hai đứa mình đã đi hẹn hò cùng nhau! 🥰\n\n*\"Mỗi ngày bên em/anh đều là một ngày đáng nhớ.\"*")
            .AddField("Đã đi iu cùng nhau", $"**{result.TotalCount}** ngày", inline: true)
            .AddField("Thời gian ghi nhận", $"{Clock.Now():HH:mm} (Giờ VN)", inline: true)
            .WithFooter($"Ghi nhận bởi {Me.Name} · Realtime sync với Web")
            .Build();

        await FollowupAsync(embed: successEmbed);
    }

    [SlashCommand("add", "Thêm một ngày hẹn hò (mặc định là hôm nay nếu để trống)")]
    public async Task AddAsync(
        [Summary("date", "Ngày theo định dạng yyyy-MM-dd hoặc dd/MM/yyyy (để trống nếu là hôm nay)")] string? date = null)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            await TodayAsync();
            return;
        }

        if (!FlexibleParser.TryParseDate(date, out var parsedDate))
            throw new UserInputException("Bot chưa đọc được ngày này. Bạn thử định dạng 2026-11-08 hoặc 08/11/2026 nha.");

        if (parsedDate > Clock.Today())
            throw new UserInputException("Ngày này chưa tới mà, sao hai đứa đi hẹn hò trước được nè! 😄");

        await DeferAsync();
        var result = await Api.MarkDatingDateAsync(Me.Username, parsedDate.ToString("yyyy-MM-dd"), Ct);

        if (result.Action == "already_exists")
        {
            var embed = Embeds.Base(Palette.Cream)
                .WithTitle("✨ Ngày này đã ghi nhận rồi nè")
                .WithDescription($"Ngày **{FormatVnDate(parsedDate)}** đã có sẵn trong lịch hẹn hò rồi! 💕\n\nTổng cộng hai đứa mình có **{result.TotalCount}** ngày đi iu cùng nhau.")
                .Build();
            await FollowupAsync(embed: embed);
            return;
        }

        var successEmbed = Embeds.Base(Palette.Pink)
            .WithTitle("💕 Đã thêm ngày hẹn hò!")
            .WithDescription($"Đã thêm ngày **{FormatVnDate(parsedDate)}** vào lịch hẹn hò của hai đứa! 🥰")
            .AddField("Đã đi iu cùng nhau", $"**{result.TotalCount}** ngày", inline: true)
            .WithFooter($"Ghi nhận bởi {Me.Name} · Realtime sync với Web")
            .Build();

        await FollowupAsync(embed: successEmbed);
    }

    [SlashCommand("detail", "Xem chi tiết thống kê và lịch hẹn hò tương tác")]
    public async Task DetailAsync(
        [Summary("year", "Lọc theo năm, vd: 2026"), MinValue(2020), MaxValue(2100)] int? year = null,
        [Summary("month", "Lọc theo tháng (1-12)"), MinValue(1), MaxValue(12)] int? month = null)
    {
        await DeferAsync();
        var y = year ?? Clock.Today().Year;
        var m = month ?? Clock.Today().Month;
        var (embed, components) = await BuildCalendarViewAsync(y, m);
        await FollowupAsync(embed: embed, components: components);
    }

    [SlashCommand("calendar", "Xem bảng lịch hẹn hò trực quan theo tháng")]
    public async Task CalendarAsync(
        [Summary("year", "Năm (vd: 2026)"), MinValue(2020), MaxValue(2100)] int? year = null,
        [Summary("month", "Tháng (1-12)"), MinValue(1), MaxValue(12)] int? month = null)
    {
        await DeferAsync();
        var y = year ?? Clock.Today().Year;
        var m = month ?? Clock.Today().Month;
        var (embed, components) = await BuildCalendarViewAsync(y, m);
        await FollowupAsync(embed: embed, components: components);
    }

    [ComponentInteraction("dating:nav:*:*:*", ignoreGroupNames: true)]
    public async Task CalendarNavigateAsync(string action, string yearStr, string monthStr)
    {
        await DeferAsync();
        if (!int.TryParse(yearStr, out var year) || !int.TryParse(monthStr, out var month))
            return;

        if (month < 1) { month = 12; year--; }
        else if (month > 12) { month = 1; year++; }

        var (embed, components) = await BuildCalendarViewAsync(year, month);
        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    [SlashCommand("remove", "Xóa một ngày khỏi lịch hẹn hò nếu đánh dấu nhầm")]
    public async Task RemoveAsync(
        [Summary("date", "Ngày theo định dạng yyyy-MM-dd hoặc dd/MM/yyyy")] string date)
    {
        if (!FlexibleParser.TryParseDate(date, out var parsedDate))
            throw new UserInputException("Bot chưa đọc được ngày này. Bạn thử định dạng 2026-11-08 hoặc 08/11/2026 nha.");

        await DeferAsync();
        var result = await Api.UnmarkDatingDateAsync(Me.Username, parsedDate.ToString("yyyy-MM-dd"), Ct);

        var embed = Embeds.Base(Palette.Cream)
            .WithTitle("🗑️ Đã xóa ngày hẹn hò")
            .WithDescription($"Đã bỏ đánh dấu ngày **{FormatVnDate(parsedDate)}** khỏi lịch hẹn hò.\n\nTổng số ngày hẹn hò hiện tại: **{result.TotalCount}** ngày.")
            .WithFooter($"Cập nhật bởi {Me.Name} · Realtime sync với Web")
            .Build();

        await FollowupAsync(embed: embed);
    }

    private async Task<(Embed Embed, MessageComponent Components)> BuildCalendarViewAsync(int year, int month)
    {
        var today = Clock.Today();
        var stats = await Api.GetDatingStatsAsync(Me.Username, year, month, Ct);

        var markedDays = stats.DatesInMonth
            .Select(d => DateOnly.TryParse(d, out var dt) ? dt.Day : 0)
            .Where(d => d > 0)
            .ToHashSet();

        var calendarAscii = RenderCalendar(year, month, markedDays, today);

        var isCurrent = today.Year == year && today.Month == month;
        var eb = Embeds.Base(Palette.Pink)
            .WithTitle($"📅 Lịch Hẹn Hò · Tháng {month:D2}/{year}")
            .WithDescription(
                $"👩‍❤️‍👨 Hai đứa mình đã cùng nhau đi hẹn hò **{stats.TotalCount}** ngày! 🌸\n\n" +
                $"{calendarAscii}\n" +
                $"*Chú thích: `[dd]` = Ngày hẹn hò · `<dd>` = Hôm nay*\n\n" +
                $"💕 **Số ngày đi chơi trong T{month:D2}/{year}:** **{markedDays.Count}** ngày" +
                (isCurrent ? $" (Năm nay: **{stats.ThisYearCount}** ngày)" : ""));

        if (stats.DatesInMonth.Count > 0)
        {
            var listStr = string.Join("\n", stats.DatesInMonth.Select(d =>
            {
                if (DateOnly.TryParse(d, out var dt))
                {
                    var mark = (today == dt) ? " 🌟 *(Hôm nay)*" : "";
                    return $"• **{dt:dd/MM/yyyy}** ({FormatVietnameseDayOfWeek(dt.DayOfWeek)}){mark}";
                }
                return $"• {d}";
            }));
            eb.AddField($"Các ngày đi chơi trong tháng {month:D2}/{year}", listStr, inline: false);
        }
        else
        {
            eb.AddField($"Tháng {month:D2}/{year}", "Chưa có ngày hẹn hò nào trong tháng này. Hãy rủ nhau đi chơi nhé! ☕🎬", inline: false);
        }

        if (stats.LatestDate != null && DateOnly.TryParse(stats.LatestDate, out var latest))
        {
            var ago = stats.DaysSinceLastDate == 0 ? "Hôm nay 🥰" : $"cách đây {stats.DaysSinceLastDate} ngày";
            eb.AddField("Lần hẹn hò gần nhất", $"**{latest:dd/MM/yyyy}** ({ago})", inline: true);
        }

        eb.WithFooter("BinVaBun · Giờ VN · Bấm nút bên dưới để chuyển tháng");

        var components = BuildNavButtons(year, month, today);
        return (eb.Build(), components);
    }

    public static string RenderCalendar(int year, int month, ISet<int> markedDays, DateOnly today)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```ansi");
        sb.AppendLine("\u001b[1;33m T2  T3  T4  T5  T6  T7  CN \u001b[0m");

        var firstDay = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var startCol = ((int)firstDay.DayOfWeek + 6) % 7;

        var col = 0;
        for (int i = 0; i < startCol; i++)
        {
            sb.Append("    ");
            col++;
        }

        for (int day = 1; day <= daysInMonth; day++)
        {
            var isMarked = markedDays.Contains(day);
            var isToday = (today.Year == year && today.Month == month && today.Day == day);

            if (isMarked)
            {
                if (isToday)
                    sb.Append($"\u001b[1;31m[{day:D2}]\u001b[0m");
                else
                    sb.Append($"\u001b[1;35m[{day:D2}]\u001b[0m");
            }
            else if (isToday)
            {
                sb.Append($"\u001b[1;36m<{day:D2}>\u001b[0m");
            }
            else
            {
                sb.Append($" {day:D2} ");
            }

            col++;
            if (col == 7)
            {
                sb.AppendLine();
                col = 0;
            }
        }

        if (col != 0)
        {
            sb.AppendLine();
        }

        sb.Append("```");
        return sb.ToString();
    }

    private static MessageComponent BuildNavButtons(int year, int month, DateOnly today)
    {
        var prevYear = month == 1 ? year - 1 : year;
        var prevMonth = month == 1 ? 12 : month - 1;
        var nextYear = month == 12 ? year + 1 : year;
        var nextMonth = month == 12 ? 1 : month + 1;

        var isCurrentMonth = today.Year == year && today.Month == month;

        var builder = new ComponentBuilder();
        builder.WithButton(
            $"◀ T{prevMonth}/{prevYear}",
            $"dating:nav:prev:{prevYear}:{prevMonth}",
            ButtonStyle.Secondary,
            row: 0);

        builder.WithButton(
            isCurrentMonth ? $"★ T{month}/{year}" : $"Hôm nay (T{today.Month}/{today.Year})",
            $"dating:nav:today:{today.Year}:{today.Month}",
            isCurrentMonth ? ButtonStyle.Success : ButtonStyle.Primary,
            disabled: isCurrentMonth,
            row: 0);

        builder.WithButton(
            $"T{nextMonth}/{nextYear} ▶",
            $"dating:nav:next:{nextYear}:{nextMonth}",
            ButtonStyle.Secondary,
            row: 0);

        return builder.Build();
    }

    private static string FormatVietnameseDayOfWeek(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday => "Thứ hai",
        DayOfWeek.Tuesday => "Thứ ba",
        DayOfWeek.Wednesday => "Thứ tư",
        DayOfWeek.Thursday => "Thứ năm",
        DayOfWeek.Friday => "Thứ sáu",
        DayOfWeek.Saturday => "Thứ bảy",
        DayOfWeek.Sunday => "Chủ nhật",
        _ => dow.ToString()
    };

    private static string FormatVnDate(DateOnly date) =>
        $"{date:dd/MM/yyyy} ({FormatVietnameseDayOfWeek(date.DayOfWeek)})";
}
