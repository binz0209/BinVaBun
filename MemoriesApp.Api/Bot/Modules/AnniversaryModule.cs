using BinVaBun.DiscordBot.Content;
using BinVaBun.DiscordBot.Hosting;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace BinVaBun.DiscordBot.Modules;

public sealed class AnniversaryAutocomplete : GuardedAutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, IAutocompleteInteraction interaction, IParameterInfo parameter, IServiceProvider services)
    {
        if (!IsAllowed(context, services, out _)) return AutocompletionResult.FromSuccess();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
            var typed = interaction.Data.Current.Value?.ToString() ?? string.Empty;
            var items = await services.GetRequiredService<BotStore>().ListAsync<Anniversary>(StoreKinds.Anniversary, limit: 100, ct: cts.Token);
            return AutocompletionResult.FromSuccess(items.Select(i => i.Value)
                .Where(a => a.Title.Contains(typed, StringComparison.OrdinalIgnoreCase))
                .Take(25).Select(a => new AutocompleteResult(Label($"{a.Title} · {a.Date:yyyy-MM-dd}"), a.Id)));
        }
        catch
        {
            return AutocompletionResult.FromSuccess();
        }
    }
}

[Group("anniversary", "Những ngày kỷ niệm quan trọng của hai đứa")]
[RequireCouple]
[CommandContextType(InteractionContextType.Guild)]
public sealed class AnniversaryModule : CompanionModuleBase
{
    public BotStore Store { get; set; } = null!;

    [SlashCommand("add", "Ghi nhớ một ngày kỷ niệm quan trọng")]
    public async Task AddAsync(
        [Summary("title", "Tên ngày kỷ niệm, vd: Ngày chính thức yêu nhau"), MaxLength(100)] string title,
        [Summary("date", "Ngày diễn ra, vd: 2024-02-14 hoặc 14/02/2024")] string date,
        [Summary("type", "Loại ngày kỷ niệm")] AnniversaryKind kind = AnniversaryKind.Other,
        [Summary("yearly", "Kỷ niệm lặp lại hàng năm (mặc định: có)")] bool yearly = true)
    {
        if (!FlexibleParser.TryParseDate(date, out var parsed))
            throw new UserInputException("Bot chưa đọc được ngày này. Bạn thử định dạng 2024-02-14 hoặc 14/02/2024 nha.");
        if (string.IsNullOrWhiteSpace(title))
            throw new UserInputException("Ngày kỷ niệm này cần có một tên gọi nha.");
        await DeferAsync();

        var existing = await Store.ListAsync<Anniversary>(StoreKinds.Anniversary, limit: 500, ct: Ct);
        if (existing.Count >= 200)
            throw new UserInputException("Đã lưu nhiều ngày kỷ niệm quá rồi nè. Hãy xóa bớt trước khi thêm mới nha.");

        var anniversary = new Anniversary
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Title = title.Trim(),
            Date = parsed,
            Kind = kind,
            Yearly = yearly,
            CreatedBy = Context.User.Id
        };
        await Store.UpsertAsync(StoreKinds.Anniversary, anniversary.Id, anniversary, Ct);

        var today = Clock.Today();
        var next = AnniversaryCalculator.NextOccurrence(anniversary, today);
        var embed = next is { } n
            ? Embeds.AnniversaryCard(anniversary, today, n, "Đã ghi vào cuốn lịch tình yêu. Bot sẽ nhắc hai đứa mình khi ngày này tới gần nha.")
            : Embeds.Notice(anniversary.Title, $"Đã lưu mốc ngày {Embeds.FormatDate(parsed)}. Ngày này đã trôi qua rồi nên bot sẽ không nhắc định kỳ nha.", Palette.Gold);
        await FollowupAsync(embed: embed);
    }

    [SlashCommand("list", "Tất cả ngày kỷ niệm, sắp xếp theo ngày gần nhất")]
    public async Task ListAsync()
    {
        await DeferAsync();
        var today = Clock.Today();
        var upcoming = (await Store.ListAsync<Anniversary>(StoreKinds.Anniversary, limit: 500, ct: Ct))
            .Select(i => i.Value)
            .Select(a => (A: a, Next: AnniversaryCalculator.NextOccurrence(a, today)))
            .OrderBy(x => x.Next ?? DateOnly.MaxValue)
            .ToList();

        if (upcoming.Count == 0)
        {
            await FollowupAsync(embed: Embeds.Plain("Chưa có ngày kỷ niệm nào. Thêm ngày đầu tiên với lệnh `/anniversary add` nha."));
            return;
        }

        var lines = upcoming.Take(25).Select(x =>
            x.Next is { } n
                ? $"**{Embeds.Truncate(x.A.Title, 60)}**\n{Embeds.FormatDate(n)} · {Countdown(AnniversaryCalculator.DaysUntil(today, n))}"
                : $"**{Embeds.Truncate(x.A.Title, 60)}**\n{Embeds.FormatDate(x.A.Date)} · đã qua");
        await FollowupAsync(embed: Embeds.Base(Palette.Gold).WithTitle("Những ngày kỷ niệm của tụi mình").WithDescription(string.Join("\n\n", lines)).Build());
    }

    [SlashCommand("next", "Ngày kỷ niệm tiếp theo sắp tới là ngày nào?")]
    public async Task NextAsync()
    {
        await DeferAsync();
        var today = Clock.Today();
        var nextUp = (await Store.ListAsync<Anniversary>(StoreKinds.Anniversary, limit: 500, ct: Ct))
            .Select(i => i.Value)
            .Select(a => (A: a, Next: AnniversaryCalculator.NextOccurrence(a, today)))
            .Where(x => x.Next.HasValue)
            .OrderBy(x => x.Next)
            .FirstOrDefault();

        if (nextUp.A == null)
        {
            await FollowupAsync(embed: Embeds.Plain("Chưa có ngày kỷ niệm nào sắp tới cả. Thêm một ngày với `/anniversary add` nha."));
            return;
        }
        await FollowupAsync(embed: Embeds.AnniversaryCard(nextUp.A, today, nextUp.Next!.Value, Voice.Pick(Voice.AnniversarySoon)));
    }

    [SlashCommand("countdown", "Thẻ đếm ngược cho một ngày kỷ niệm")]
    public async Task CountdownAsync(
        [Summary("date", "Chọn một ngày kỷ niệm của tụi mình"), Autocomplete(typeof(AnniversaryAutocomplete))] string anniversary)
    {
        await DeferAsync();
        var a = await Resolve(anniversary);
        var today = Clock.Today();
        var next = AnniversaryCalculator.NextOccurrence(a, today);
        if (next is not { } n)
        {
            await FollowupAsync(embed: Embeds.Notice(a.Title, $"Ngày này là vào {Embeds.FormatDate(a.Date)}. Nó đã trôi qua rồi nè.", Palette.Gold));
            return;
        }

        var card = Embeds.AnniversaryCard(a, today, n, AnniversaryCalculator.DaysUntil(today, n) == 0 ? Voice.Pick(Voice.AnniversaryToday) : Voice.Pick(Voice.AnniversarySoon))
            .ToEmbedBuilder();
        if (a.Date <= today)
            card.AddField("Đã bên nhau được", $"{AnniversaryCalculator.DaysUntil(a.Date, today):N0} ngày");
        await FollowupAsync(embed: card.Build());
    }

    [SlashCommand("delete", "Xóa một ngày kỷ niệm")]
    public async Task DeleteAsync(
        [Summary("date", "Chọn ngày kỷ niệm muốn xóa"), Autocomplete(typeof(AnniversaryAutocomplete))] string anniversary)
    {
        await DeferAsync(ephemeral: true);
        var a = await Resolve(anniversary);
        await Store.DeleteAsync(StoreKinds.Anniversary, a.Id, Ct);
        await FollowupAsync(embed: Embeds.Plain($"Đã xóa ngày kỷ niệm **{Embeds.Truncate(a.Title, 80)}** rồi nè."), ephemeral: true);
    }

    private async Task<Anniversary> Resolve(string idOrTitle)
    {
        var all = await Store.ListAsync<Anniversary>(StoreKinds.Anniversary, limit: 500, ct: Ct);
        return all.Select(i => i.Value).FirstOrDefault(a => a.Id == idOrTitle)
               ?? all.Select(i => i.Value).FirstOrDefault(a => a.Title.Equals(idOrTitle, StringComparison.OrdinalIgnoreCase))
               ?? throw new UserInputException("Bot không tìm thấy ngày kỷ niệm này. Hãy chọn từ danh sách gợi ý nha.");
    }

    private static string Countdown(int days) => days switch { 0 => "hôm nay", 1 => "ngày mai", _ => $"còn {days} ngày nữa" };
}

