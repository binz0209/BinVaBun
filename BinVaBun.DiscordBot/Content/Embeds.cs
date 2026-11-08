using System.Globalization;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;

namespace BinVaBun.DiscordBot.Content;

public static class Palette
{
    public static readonly Color Brown = new(0x9C, 0x7A, 0x5B);   // soft brown (default)
    public static readonly Color Cream = new(0xEF, 0xDF, 0xC6);   // warm cream
    public static readonly Color Pink = new(0xD4, 0xA0, 0xA0);    // muted pink (notes, questions)
    public static readonly Color Gold = new(0xC9, 0xA2, 0x4B);    // subtle gold (anniversaries)
}

public static class Embeds
{
    public static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";
    }

    public static string FormatDate(DateOnly date) => $"{date.Day} tháng {date.Month}, {date.Year}";

    public static EmbedBuilder Base(Color? color = null) =>
        new EmbedBuilder().WithColor(color ?? Palette.Brown);

    public static Embed Notice(string title, string description, Color? color = null) =>
        Base(color ?? Palette.Cream).WithTitle(title).WithDescription(description).Build();

    public static Embed Plain(string description, Color? color = null) =>
        Base(color ?? Palette.Cream).WithDescription(description).Build();

    public const int ImagesPerPage = 4;

    public static (Embed[] Embeds, MessageComponent? Components) MemoryCards(
        Memory m,
        string? lead = null,
        Color? color = null,
        int photoPage = 0,
        bool includeAgainButton = false,
        bool againPhotoOnly = false)
    {
        var totalImages = m.Images.Count;
        var totalPages = totalImages > 0 ? (int)Math.Ceiling((double)totalImages / ImagesPerPage) : 1;
        var safePage = Math.Clamp(photoPage, 0, Math.Max(0, totalPages - 1));

        var pageImages = totalImages > 0
            ? m.Images.Skip(safePage * ImagesPerPage).Take(ImagesPerPage).ToList()
            : [];

        // In Discord, when multiple embeds in the same message share the exact same Url,
        // the Discord client visually collapses them into a unified multi-image collage/mosaic.
        var cardUrl = safePage == 0 ? $"https://binvabun.vercel.app/post/{m.Id}" : $"https://binvabun.vercel.app/post/{m.Id}?p={safePage}";

        var body = m.ContentIsTitle ? string.Empty : Truncate(m.Content, 3500);
        var footerText = totalImages switch
        {
            0 => $"Lưu giữ bởi {m.AuthorName}",
            1 => $"Lưu giữ bởi {m.AuthorName} · 1 ảnh",
            <= 4 => $"Lưu giữ bởi {m.AuthorName} · {totalImages} ảnh",
            _ => $"Lưu giữ bởi {m.AuthorName} · {totalImages} ảnh (Trang ảnh {safePage + 1}/{totalPages})"
        };

        var mainEb = Base(color)
            .WithTitle(Truncate(m.Title, 250))
            .WithUrl(cardUrl)
            .WithFooter(footerText);

        var description = string.Join("\n\n", new[] { lead is null ? null : $"*{lead}*", body }.Where(s => !string.IsNullOrEmpty(s)));
        if (description.Length > 0) mainEb.WithDescription(description);

        mainEb.AddField("Thời gian", FormatDate(m.DateValue), inline: true);
        if (m.Tags.Count > 0)
            mainEb.AddField("Thẻ", Truncate(string.Join(" ", m.Tags.Select(t => $"`#{t}`")), 1000), inline: true);

        if (pageImages.Count > 0)
            mainEb.WithImageUrl(pageImages[0]);

        var embeds = new List<Embed> { mainEb.Build() };

        for (int i = 1; i < pageImages.Count; i++)
        {
            var extraEb = Base(color)
                .WithUrl(cardUrl)
                .WithImageUrl(pageImages[i]);
            embeds.Add(extraEb.Build());
        }

        ComponentBuilder? builder = null;
        var againFlag = includeAgainButton ? (againPhotoOnly ? "1" : "0") : "n";

        if (totalPages > 1)
        {
            builder ??= new ComponentBuilder();
            var startIdx = safePage * ImagesPerPage + 1;
            var endIdx = Math.Min(totalImages, (safePage + 1) * ImagesPerPage);

            builder.WithButton("◀ Ảnh trước", $"mem:img:{m.Id}:{safePage - 1}:{againFlag}", ButtonStyle.Secondary, disabled: safePage <= 0, row: 0);
            builder.WithButton($"Ảnh {startIdx}-{endIdx} / {totalImages}", "mem:noop", ButtonStyle.Primary, disabled: true, row: 0);
            builder.WithButton("Ảnh sau ▶", $"mem:img:{m.Id}:{safePage + 1}:{againFlag}", ButtonStyle.Secondary, disabled: safePage >= totalPages - 1, row: 0);
        }

        if (includeAgainButton)
        {
            builder ??= new ComponentBuilder();
            var row = totalPages > 1 ? 1 : 0;
            builder.WithButton("Xem kỷ niệm khác", $"mem:again:{(againPhotoOnly ? 1 : 0)}", ButtonStyle.Secondary, row: row);
        }

        return (embeds.ToArray(), builder?.Build());
    }

    public static Embed MemoryCard(Memory m, string? lead = null, Color? color = null) =>
        MemoryCards(m, lead, color).Embeds[0];

    public static Embed MemoryListPage(PagerState state, MemoryPage page)
    {
        var lines = page.Items.Select((m, i) =>
        {
            var n = (page.Page - 1) * page.PageSize + i + 1;
            var tags = m.Tags.Count > 0 ? "  " + string.Join(" ", m.Tags.Take(3).Select(t => $"`#{t}`")) : string.Empty;
            var photo = m.Images.Count > 0 ? " · ảnh" : string.Empty;
            var excerpt = m.ContentIsTitle ? string.Empty : "\n" + Truncate(m.Content.ReplaceLineEndings(" "), 90);
            return $"**{n}. {Truncate(m.Title, 70)}**\n{FormatDate(m.DateValue)}{photo}{tags}{excerpt}";
        });
        return Base()
            .WithTitle(state.Heading)
            .WithDescription(page.Items.Count == 0 ? "Chưa có kỷ niệm nào ở đây cả." : string.Join("\n\n", lines))
            .WithFooter($"Trang {Math.Max(1, page.Page)} / {Math.Max(1, page.TotalPages)} · {page.TotalCount} kỷ niệm")
            .Build();
    }

    public static Embed AnniversaryCard(Anniversary a, DateOnly today, DateOnly next, string? lead = null)
    {
        var days = AnniversaryCalculator.DaysUntil(today, next);
        var when = days switch { 0 => "Hôm nay", 1 => "Ngày mai", _ => $"Còn {days} ngày nữa" };
        var eb = Base(Palette.Gold)
            .WithTitle(Truncate(a.Title, 250))
            .AddField("Ngày kỷ niệm", FormatDate(next), inline: true)
            .AddField("Đếm ngược", when, inline: true);
        if (lead != null) eb.WithDescription($"*{lead}*");
        if (a.Yearly && next.Year > a.Date.Year)
            eb.AddField("Cột mốc", $"Năm thứ {AnniversaryCalculator.YearsAt(a, next)}", inline: true);
        if (days > 14)
        {
            var (y, m, d) = AnniversaryCalculator.Breakdown(today, next);
            var parts = new List<string>();
            if (y > 0) parts.Add($"{y} năm");
            if (m > 0) parts.Add($"{m} tháng");
            if (d > 0) parts.Add($"{d} ngày");
            eb.WithFooter($"Còn {string.Join(" ", parts)} nữa là tới");
        }
        return eb.Build();
    }
}
