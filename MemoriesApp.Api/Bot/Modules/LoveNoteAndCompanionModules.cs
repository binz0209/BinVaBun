using BinVaBun.DiscordBot.BackgroundServices;
using BinVaBun.DiscordBot.Content;
using BinVaBun.DiscordBot.Hosting;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;
using Discord.Interactions;

namespace BinVaBun.DiscordBot.Modules;

[Group("lovenote", "Những lá thư tình ngọt ngào gửi riêng cho người ấy")]
[RequireCouple]
[CommandContextType(InteractionContextType.Guild)]
public sealed class LoveNoteModule : CompanionModuleBase
{
    public BotStore Store { get; set; } = null!;
    public LoveNoteDelivery Delivery { get; set; } = null!;

    [SlashCommand("send", "Gửi một bức thư ngọt ngào cho đối phương")]
    public async Task SendAsync(
        [Summary("message", "Nội dung bạn muốn nhắn nhủ"), MaxLength(1500)] string message,
        [Summary("deliver_at", "Tùy chọn thời gian gửi: 2026-12-24 07:00, tomorrow 09:00, in 3h (mặc định: gửi ngay)")] string? deliverAt = null)
    {
        var partner = Owners.PartnerOf(Me.DiscordId)
                      ?? throw new UserInputException("Tính năng thư tình cần đúng hai người trong danh sách của bot nha.");
        if (string.IsNullOrWhiteSpace(message)) throw new UserInputException("Lá thư cần có vài dòng nhắn gửi chứ nè.");

        var now = Clock.Now();
        var when = Clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(deliverAt))
        {
            if (!FlexibleParser.TryParseDateTime(deliverAt, now, Clock.Zone, out var parsed))
                throw new UserInputException("Bot chưa đọc được mốc thời gian này. Bạn thử: `2026-12-24 07:00`, `tomorrow 09:00` hoặc `in 3h` nha.");
            if (parsed > now.AddYears(1)) throw new UserInputException("Hãy hẹn gửi trong vòng 1 năm tới thôi nha.");
            when = parsed < now ? Clock.UtcNow : parsed;
        }
        await DeferAsync(ephemeral: true);

        var note = new LoveNote
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            From = Me.DiscordId,
            To = partner.DiscordId,
            Text = message.Trim(),
            CreatedAt = Clock.UtcNow,
            DeliverAt = when
        };
        await Store.UpsertAsync(StoreKinds.LoveNote, note.Id, note, Ct);

        if (when <= Clock.UtcNow.AddSeconds(5))
        {
            await Delivery.DeliverAsync(note, Context.Channel.Id, Ct);
            await FollowupAsync(embed: Embeds.Plain($"Đã chuyển thư tới tận tay {partner.Name} rồi nè."), ephemeral: true);
        }
        else
        {
            var local = TimeZoneInfo.ConvertTime(when, Clock.Zone);
            await FollowupAsync(embed: Embeds.Plain($"Bot sẽ giao thư cho {partner.Name} vào lúc **{local:HH:mm - dd/MM/yyyy}** nha."), ephemeral: true);
        }
    }

    [SlashCommand("inbox", "Hộp thư đến: đọc những bức thư người ấy gửi cho bạn")]
    public async Task InboxAsync()
    {
        await DeferAsync(ephemeral: true);
        var mine = (await Store.ListAsync<LoveNote>(StoreKinds.LoveNote, limit: 500, ct: Ct))
            .Select(i => i.Value)
            .Where(n => n.To == Context.User.Id && n.Delivered)   // only ever your own notes
            .OrderByDescending(n => n.DeliverAt).Take(5).ToList();

        if (mine.Count == 0)
        {
            await FollowupAsync(embed: Embeds.Plain("Hiện tại chưa có bức thư nào đang chờ bạn cả."), ephemeral: true);
            return;
        }

        var embeds = mine.Select(n =>
        {
            var from = Owners.TryGet(n.From, out var f) ? f.Name : "Ai đó";
            var local = TimeZoneInfo.ConvertTime(n.DeliverAt, Clock.Zone);
            return Embeds.Base(Palette.Pink).WithTitle($"Thư gửi từ {from}").WithDescription(Embeds.Truncate(n.Text, 3000))
                .WithFooter($"{local:HH:mm - dd/MM/yyyy}").Build();
        }).ToArray();
        await FollowupAsync(embeds: embeds, ephemeral: true);

        foreach (var n in mine.Where(n => n.ReadAt == null))
        {
            n.ReadAt = Clock.UtcNow;
            await Store.UpsertAsync(StoreKinds.LoveNote, n.Id, n, Ct);
        }
    }
}

[Group("companion", "Cài đặt và thiết lập cho bot")]
[RequireCouple]
[CommandContextType(InteractionContextType.Guild)]
public sealed class CompanionModule : CompanionModuleBase
{
    public ScheduleSettingsService Settings { get; set; } = null!;

    [SlashCommand("setup", "Chọn kênh và khung giờ bot gửi thông báo tự động")]
    public async Task SetupAsync(
        [Summary("channel", "Kênh thông báo kỷ niệm và lời nhắc"), ChannelTypes(ChannelType.Text)] ITextChannel? channel = null,
        [Summary("memory_time", "Giờ đăng kỷ niệm hàng ngày, định dạng HH:mm")] string? memoryTime = null,
        [Summary("question_time", "Giờ gửi câu hỏi hàng ngày, định dạng HH:mm")] string? questionTime = null,
        [Summary("anniversary_time", "Giờ nhắc ngày kỷ niệm, định dạng HH:mm")] string? anniversaryTime = null,
        [Summary("daily_memory", "Bật tính năng nhắc kỷ niệm mỗi ngày")] bool? dailyMemory = null,
        [Summary("daily_question", "Bật tính năng câu hỏi mỗi ngày")] bool? dailyQuestion = null)
    {
        foreach (var t in new[] { memoryTime, questionTime, anniversaryTime }.Where(t => t != null))
            if (!FlexibleParser.TryParseTimeOfDay(t, out _))
                throw new UserInputException("Định dạng giờ có dạng như `20:00` (chuẩn 24 giờ) nha.");
        await DeferAsync(ephemeral: true);

        var s = await Settings.GetStoredAsync(Ct, forceRefresh: true);
        if (channel != null) s.ChannelId = channel.Id;
        if (memoryTime != null) s.DailyMemoryTime = memoryTime.Trim();
        if (questionTime != null) s.DailyQuestionTime = questionTime.Trim();
        if (anniversaryTime != null) s.AnniversaryTime = anniversaryTime.Trim();
        if (dailyMemory != null) s.DailyMemoryEnabled = dailyMemory.Value;
        if (dailyQuestion != null) s.DailyQuestionEnabled = dailyQuestion.Value;
        await Settings.SaveAsync(s, Ct);

        await FollowupAsync(embed: await StatusEmbedAsync("Đã lưu thiết lập thành công! Dưới đây là cấu hình hiện tại của bot:"), ephemeral: true);
    }

    [SlashCommand("status", "Xem trạng thái hoạt động và kết nối của bot")]
    public async Task StatusAsync()
    {
        await DeferAsync(ephemeral: true);
        await FollowupAsync(embed: await StatusEmbedAsync(null), ephemeral: true);
    }

    private async Task<Embed> StatusEmbedAsync(string? lead)
    {
        var e = await Settings.GetEffectiveAsync(Ct);
        string service;
        try
        {
            await Api.GetMeAsync(Me.Username, Ct);
            service = "Đang kết nối tốt";
        }
        catch (CompanionException ex)
        {
            service = ex.UserMessage;
        }

        var eb = Embeds.Base().WithTitle("Trạng thái hoạt động của bot");
        if (lead != null) eb.WithDescription(lead);
        eb.AddField("Kênh thông báo", e.ChannelId is { } c ? $"<#{c}>" : "Chưa thiết lập. Dùng `/companion setup` nha", inline: true)
          .AddField("Kỷ niệm mỗi ngày", e.DailyMemoryEnabled ? $"{e.DailyMemory:HH\\:mm}" : "Tắt", inline: true)
          .AddField("Câu hỏi mỗi ngày", e.DailyQuestionEnabled ? $"{e.DailyQuestion:HH\\:mm}" : "Tắt", inline: true)
          .AddField("Nhắc ngày kỷ niệm", $"{e.Anniversary:HH\\:mm}", inline: true)
          .AddField("Múi giờ", Clock.Zone.Id, inline: true)
          .AddField("Hộp kỷ niệm (API)", service, inline: true);
        return eb.Build();
    }
}
