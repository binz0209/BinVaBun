using BinVaBun.DiscordBot.Content;
using BinVaBun.DiscordBot.Hosting;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace BinVaBun.DiscordBot.Modules;

public sealed class ReminderAutocomplete : GuardedAutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, IAutocompleteInteraction interaction, IParameterInfo parameter, IServiceProvider services)
    {
        if (!IsAllowed(context, services, out _)) return AutocompletionResult.FromSuccess();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
            var zone = services.GetRequiredService<IClock>().Zone;
            var items = await services.GetRequiredService<BotStore>().ListAsync<PersonalReminder>(StoreKinds.Reminder, limit: 100, ct: cts.Token);
            return AutocompletionResult.FromSuccess(items.Select(i => i.Value)
                .Where(r => RemindModule.CanSee(r, context.User.Id))
                .OrderBy(r => r.DueAt).Take(25)
                .Select(r => new AutocompleteResult(
                    Label($"{TimeZoneInfo.ConvertTime(r.DueAt, zone):dd/MM/yyyy HH:mm} · {r.Text}"), r.Id)));
        }
        catch
        {
            return AutocompletionResult.FromSuccess();
        }
    }
}

[Group("remind", "Những lời hẹn và lời nhắc nhỏ cho hai đứa mình")]
[RequireCouple]
[CommandContextType(InteractionContextType.Guild)]
public sealed class RemindModule : CompanionModuleBase
{
    public BotStore Store { get; set; } = null!;

    public enum Audience
    {
        [ChoiceDisplay("Cả hai đứa")] Both,
        [ChoiceDisplay("Chỉ riêng mình")] Me
    }

    /// <summary>"Just me" reminders belong to their creator; shared ones are visible to both.</summary>
    public static bool CanSee(PersonalReminder r, ulong userId) => r.ForBoth || r.CreatedBy == userId;

    [SlashCommand("add", "Nhờ bot nhắc nhở một việc quan trọng")]
    public async Task AddAsync(
        [Summary("what", "Bạn muốn bot nhắc việc gì?"), MaxLength(500)] string what,
        [Summary("when", "vd: 2026-12-24 19:30, 24/12/2026 19:30, tomorrow 09:00, in 2h")] string when,
        [Summary("for", "Nhắc ai nè?")] Audience audience = Audience.Both)
    {
        if (string.IsNullOrWhiteSpace(what)) throw new UserInputException("Bạn muốn bot nhắc việc gì nè?");
        var now = Clock.Now();
        if (!FlexibleParser.TryParseDateTime(when, now, Clock.Zone, out var due))
            throw new UserInputException("Bot chưa đọc được mốc thời gian này. Bạn thử: `2026-12-24 19:30`, `24/12/2026 19:30`, `tomorrow 09:00` hoặc `in 2h` nha.");
        if (due <= now.AddSeconds(-30))
            throw new UserInputException("Thời gian này đã trôi qua rồi nè.");
        await DeferAsync(ephemeral: true);

        var existing = await Store.ListAsync<PersonalReminder>(StoreKinds.Reminder, limit: 500, ct: Ct);
        if (existing.Count >= 200)
            throw new UserInputException("Đang có khá nhiều lời nhắc chờ rồi. Hãy hoàn thành bớt trước khi tạo thêm nha.");

        var reminder = new PersonalReminder
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Text = what.Trim(),
            DueAt = due,
            ForBoth = audience == Audience.Both,
            CreatedBy = Context.User.Id
        };
        await Store.UpsertAsync(StoreKinds.Reminder, reminder.Id, reminder, Ct);
        await FollowupAsync(embed: Embeds.Plain($"Bot sẽ nhắc {(reminder.ForBoth ? "cả hai đứa" : "bạn")} vào lúc **{due:HH:mm - dd/MM/yyyy}** nha."), ephemeral: true);
    }

    [SlashCommand("list", "Danh sách các lời nhắc đang chờ")]
    public async Task ListAsync()
    {
        await DeferAsync(ephemeral: true);
        var mine = (await Store.ListAsync<PersonalReminder>(StoreKinds.Reminder, limit: 500, ct: Ct))
            .Select(i => i.Value).Where(r => CanSee(r, Context.User.Id)).OrderBy(r => r.DueAt).Take(25).ToList();
        if (mine.Count == 0)
        {
            await FollowupAsync(embed: Embeds.Plain("Hiện không có lời nhắc nào đang chờ cả. Thêm mới với `/remind add` nha."), ephemeral: true);
            return;
        }
        var lines = mine.Select(r =>
            $"**{TimeZoneInfo.ConvertTime(r.DueAt, Clock.Zone):HH:mm - dd/MM/yyyy}** · {(r.ForBoth ? "Cả hai đứa" : "Chỉ riêng bạn")}\n{Embeds.Truncate(r.Text, 120)}");
        await FollowupAsync(embed: Embeds.Base().WithTitle("Những lời nhắc đang chờ").WithDescription(string.Join("\n\n", lines)).Build(), ephemeral: true);
    }

    [SlashCommand("delete", "Hủy một lời nhắc")]
    public async Task DeleteAsync(
        [Summary("reminder", "Chọn lời nhắc muốn hủy"), Autocomplete(typeof(ReminderAutocomplete))] string reminder)
    {
        await DeferAsync(ephemeral: true);
        var all = await Store.ListAsync<PersonalReminder>(StoreKinds.Reminder, limit: 500, ct: Ct);
        var target = all.Select(i => i.Value).FirstOrDefault(r => r.Id == reminder && CanSee(r, Context.User.Id))
                     ?? throw new UserInputException("Bot không tìm thấy lời nhắc này.");
        await Store.DeleteAsync(StoreKinds.Reminder, target.Id, Ct);
        await FollowupAsync(embed: Embeds.Plain("Đã hủy lời nhắc thành công nè."), ephemeral: true);
    }
}
