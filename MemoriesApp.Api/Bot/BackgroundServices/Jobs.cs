using BinVaBun.DiscordBot.Configuration;
using BinVaBun.DiscordBot.Content;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BinVaBun.DiscordBot.BackgroundServices;

/// <summary>Component ids shared between scheduled posts and the modules that handle the interactions.</summary>
public static class CustomIds
{
    public const string DailyAnswerButton = "question:answer:";   // + yyyy-MM-dd
    public const string DailyAnswerModal = "question:modal:";     // + yyyy-MM-dd
}

public sealed class DailyMemoryJob(
    MemoriesApiClient api, SentLedger ledger, DiscordNotifier notifier, OwnerDirectory owners,
    ILogger<DailyMemoryJob> logger) : IScheduledJob
{
    private const string Prefix = "dailymemory:";
    public string Name => "daily-memory";

    public async Task RunAsync(JobContext ctx, CancellationToken ct)
    {
        if (!ctx.Settings.DailyMemoryEnabled || ctx.Settings.ChannelId is not { } channel) return;
        if (!ScheduleMath.IsDue(ctx.LocalNow, ctx.Settings.DailyMemory, ctx.CatchUp)) return;

        var key = Prefix + ctx.Today.ToString("yyyy-MM-dd");
        var user = owners.System.Username;

        // Decide what to post first (cheap, read-only); the claim happens right before sending.
        var onThisDay = await api.OnThisDayAsync(user, ctx.Today, ct);
        Memory? memory;
        string lead;
        if (onThisDay.Count > 0)
        {
            memory = Voice.Pick(onThisDay);
            lead = Voice.Pick(Voice.OnThisDay);
        }
        else
        {
            var recent = await ledger.RecentMemoryIdsAsync(Prefix, 14, ct);
            memory = await api.GetRandomAsync(user, hasImage: false, recent, ct);
            lead = Voice.Pick(Voice.Found);
        }

        Embed embed;
        if (memory == null)
        {
            embed = Embeds.Notice("Hộp thời gian của tụi mình", Voice.Pick(Voice.NothingYet));
        }
        else
        {
            var card = Embeds.MemoryCard(memory, lead);
            var latest = await api.ListAsync(user, new MemoryQuery(PageSize: 1), ct);
            var stale = latest.Items.FirstOrDefault() is { } newest && ctx.Today.DayNumber - newest.DateValue.DayNumber >= 14;
            if (stale)
                card = card.ToEmbedBuilder().AddField("Một lời nhắc nhỏ", Voice.Pick(Voice.NudgeNewMemory) + " Dùng `/memory save` nha.").Build();
            embed = card;
        }

        var sent = await ledger.RunOnceAsync(key,
            () => notifier.SendToChannelAsync(channel, embeds: [embed], mentionUsers: owners.All.Select(o => o.DiscordId)),
            memory?.Id, ct);
        if (sent) logger.LogInformation("Daily memory posted for {Date}", ctx.Today);
    }
}

public sealed class DailyQuestionJob(
    SentLedger ledger, DiscordNotifier notifier, ILogger<DailyQuestionJob> logger) : IScheduledJob
{
    public string Name => "daily-question";

    public async Task RunAsync(JobContext ctx, CancellationToken ct)
    {
        if (!ctx.Settings.DailyQuestionEnabled || ctx.Settings.ChannelId is not { } channel) return;
        if (!ScheduleMath.IsDue(ctx.LocalNow, ctx.Settings.DailyQuestion, ctx.CatchUp)) return;

        var date = ctx.Today.ToString("yyyy-MM-dd");
        var embed = Embeds.Base(Palette.Pink)
            .WithTitle("Câu hỏi hôm nay")
            .WithDescription($"*{Voice.Pick(Voice.DailyQuestionLead)}*\n\n**{QuestionBank.ForDate(ctx.Today)}**")
            .WithFooter("Câu trả lời sẽ được giữ bí mật cho đến khi cả hai cùng trả lời.")
            .Build();
        var components = new ComponentBuilder()
            .WithButton("Trả lời", CustomIds.DailyAnswerButton + date, ButtonStyle.Secondary)
            .Build();

        var sent = await ledger.RunOnceAsync("dailyquestion:" + date,
            () => notifier.SendToChannelAsync(channel, embeds: [embed], components: components), ct: ct);
        if (sent) logger.LogInformation("Daily question posted for {Date}", date);
    }
}

public sealed class AnniversaryJob(
    BotStore store, SentLedger ledger, DiscordNotifier notifier, OwnerDirectory owners,
    IOptions<CompanionOptions> options, ILogger<AnniversaryJob> logger) : IScheduledJob
{
    public string Name => "anniversaries";

    public async Task RunAsync(JobContext ctx, CancellationToken ct)
    {
        if (ctx.Settings.ChannelId is not { } channel) return;
        if (!ScheduleMath.IsDue(ctx.LocalNow, ctx.Settings.Anniversary, ctx.CatchUp)) return;

        var offsets = options.Value.ParseReminderDays();
        var all = await store.ListAsync<Anniversary>(StoreKinds.Anniversary, limit: 500, ct: ct);

        foreach (var (_, a) in all)
        {
            if (AnniversaryCalculator.NextOccurrence(a, ctx.Today) is not { } next) continue;
            var daysLeft = AnniversaryCalculator.DaysUntil(ctx.Today, next);
            if (!offsets.Contains(daysLeft)) continue;

            var key = $"anniv:{a.Id}:{next:yyyy-MM-dd}:{daysLeft}";
            var lead = Voice.Pick(daysLeft == 0 ? Voice.AnniversaryToday : Voice.AnniversarySoon);
            var embed = Embeds.AnniversaryCard(a, ctx.Today, next, lead);

            var sent = await ledger.RunOnceAsync(key,
                () => notifier.SendToChannelAsync(channel, embeds: [embed], mentionUsers: owners.All.Select(o => o.DiscordId)), ct: ct);
            if (sent) logger.LogInformation("Anniversary reminder sent ({DaysLeft} days before)", daysLeft);
        }
    }
}

public sealed class ReminderJob(
    BotStore store, SentLedger ledger, DiscordNotifier notifier, OwnerDirectory owners, IClock clock,
    ILogger<ReminderJob> logger) : IScheduledJob
{
    public string Name => "reminders";

    public async Task RunAsync(JobContext ctx, CancellationToken ct)
    {
        if (ctx.Settings.ChannelId is not { } channel) return;

        var reminders = await store.ListAsync<PersonalReminder>(StoreKinds.Reminder, limit: 500, ct: ct);
        foreach (var (_, r) in reminders.Where(x => x.Value.DueAt <= clock.UtcNow))
        {
            var late = clock.UtcNow - r.DueAt > TimeSpan.FromHours(1);
            var embed = Embeds.Base(Palette.Brown)
                .WithTitle("Một lời nhắc nhẹ nhàng")
                .WithDescription($"*{Voice.Pick(Voice.ReminderLead)}*\n\n{Embeds.Truncate(r.Text, 1500)}")
                .WithFooter(late ? "Xin lỗi hai đứa, lời nhắc này đến hơi muộn một chút." : "Được đặt bởi " + (owners.TryGet(r.CreatedBy, out var by) ? by.Name : "một trong hai đứa"))
                .Build();
            var mentions = r.ForBoth ? owners.All.Select(o => o.DiscordId) : [r.CreatedBy];

            var sent = await ledger.RunOnceAsync("reminder:" + r.Id,
                () => notifier.SendToChannelAsync(channel, embeds: [embed], mentionUsers: mentions), ct: ct);
            // Delete whether we sent it now or an earlier attempt already did (claim exists), so nothing lingers.
            await store.DeleteAsync(StoreKinds.Reminder, r.Id, ct);
            if (sent) logger.LogInformation("Reminder {ReminderId} delivered", r.Id);
        }
    }
}

public sealed class LoveNoteJob(LoveNoteDelivery delivery, BotStore store, IClock clock) : IScheduledJob
{
    public string Name => "love-notes";

    public async Task RunAsync(JobContext ctx, CancellationToken ct)
    {
        var notes = await store.ListAsync<LoveNote>(StoreKinds.LoveNote, limit: 500, ct: ct);
        foreach (var (_, note) in notes.Where(n => !n.Value.Delivered && n.Value.DeliverAt <= clock.UtcNow))
            await delivery.DeliverAsync(note, ctx.Settings.ChannelId, ct);
    }
}

/// <summary>Delivers a love note by DM; if DMs are closed it leaves the note in the inbox and says so (without the text).</summary>
public sealed class LoveNoteDelivery(
    BotStore store, SentLedger ledger, DiscordNotifier notifier, OwnerDirectory owners,
    ILogger<LoveNoteDelivery> logger)
{
    public async Task DeliverAsync(LoveNote note, ulong? fallbackChannel, CancellationToken ct)
    {
        var from = owners.TryGet(note.From, out var f) ? f.Name : "Ai đó";
        var embed = Embeds.Base(Palette.Pink)
            .WithTitle($"Một bức thư từ {from}")
            .WithDescription(Embeds.Truncate(note.Text, 3500))
            .WithFooter("Dành riêng cho bạn.")
            .Build();

        await ledger.RunOnceAsync("lovenote:" + note.Id, async () =>
        {
            var dmOk = await notifier.TrySendDmAsync(note.To, embed);
            if (!dmOk && fallbackChannel is { } channel)
            {
                // Never put the note itself in a shared channel.
                await notifier.SendToChannelAsync(channel,
                    text: $"<@{note.To}> ơi, có một bức thư tình từ {from} đang chờ bạn nè! Hãy mở bằng lệnh `/lovenote inbox` nha.",
                    mentionUsers: [note.To]);
            }
            note.Delivered = true;
            await store.UpsertAsync(StoreKinds.LoveNote, note.Id, note, ct);
            logger.LogInformation("Love note {NoteId} delivered (dm={Dm})", note.Id, dmOk);
        }, ct: ct);
    }
}
