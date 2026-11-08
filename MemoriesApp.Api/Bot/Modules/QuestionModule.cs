using BinVaBun.DiscordBot.BackgroundServices;
using BinVaBun.DiscordBot.Content;
using BinVaBun.DiscordBot.Hosting;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;
using Discord.Interactions;

namespace BinVaBun.DiscordBot.Modules;

public sealed class AnswerModal : IModal
{
    public string Title => "Câu trả lời của tụi mình";

    [InputLabel("Câu trả lời của bạn")]
    [ModalTextInput("answer", TextInputStyle.Paragraph, "Cứ từ từ viết nha…", minLength: 1, maxLength: 1000)]
    public string Answer { get; set; } = string.Empty;
}

[Group("question", "Một câu hỏi nhỏ cho hai đứa mình mỗi ngày")]
[RequireCouple]
[CommandContextType(InteractionContextType.Guild)]
public sealed class QuestionModule : CompanionModuleBase
{
    public BotStore Store { get; set; } = null!;

    private static string Key(string date, Owner o) => $"{date}:{o.KeyToken}";

    [SlashCommand("today", "Câu hỏi của ngày hôm nay")]
    public async Task TodayAsync()
    {
        var today = Clock.Today();
        var embed = Embeds.Base(Palette.Pink)
            .WithTitle("Câu hỏi hôm nay")
            .WithDescription($"*{Voice.Pick(Voice.DailyQuestionLead)}*\n\n**{QuestionBank.ForDate(today)}**")
            .WithFooter("Câu trả lời sẽ được giữ bí mật cho đến khi cả hai cùng trả lời.")
            .Build();
        var components = new ComponentBuilder()
            .WithButton("Trả lời", CustomIds.DailyAnswerButton + today.ToString("yyyy-MM-dd"), ButtonStyle.Secondary)
            .Build();
        await RespondAsync(embed: embed, components: components);
    }

    [ComponentInteraction(CustomIds.DailyAnswerButton + "*", ignoreGroupNames: true)]
    public async Task OpenAnswerAsync(string date)
    {
        ValidateDate(date);
        await RespondWithModalAsync<AnswerModal>(CustomIds.DailyAnswerModal + date);
    }

    [ModalInteraction(CustomIds.DailyAnswerModal + "*")]
    public async Task SubmitAnswerAsync(string date, AnswerModal modal)
    {
        var day = ValidateDate(date);
        await DeferAsync(ephemeral: true);

        var answer = new DailyAnswer
        {
            Date = date,
            DiscordId = Context.User.Id,
            Name = Me.Name,
            Answer = modal.Answer.Trim(),
            AnsweredAt = Clock.UtcNow
        };
        await Store.UpsertAsync(StoreKinds.DailyAnswer, Key(date, Me), answer, Ct);

        var partner = Owners.PartnerOf(Me.DiscordId);
        var theirs = partner == null ? null : await Store.GetAsync<DailyAnswer>(StoreKinds.DailyAnswer, Key(date, partner), Ct);
        if (theirs == null)
        {
            var who = partner?.Name ?? "đối phương";
            await FollowupAsync(embed: Embeds.Plain($"Đã lưu câu trả lời. Bot sẽ giữ bí mật cho đến khi {who} trả lời luôn nha."), ephemeral: true);
            return;
        }

        await FollowupAsync(embed: Embeds.Plain("Đã lưu câu trả lời. Cả hai đứa đều đã trả lời rồi nè, xem đáp án thôi!"), ephemeral: true);
        if (await Store.TryClaimAsync(StoreKinds.DailyReveal, date, new { at = Clock.UtcNow }, Ct))
            await Context.Channel.SendMessageAsync(embed: RevealEmbed(day, answer, theirs));
    }

    [SlashCommand("reveal", "Xem câu trả lời hôm nay (hoặc ngày trước đó)")]
    public async Task RevealAsync([Summary("date", "yyyy-MM-dd, mặc định là hôm nay")] string? date = null)
    {
        var today = Clock.Today();
        var day = today;
        if (!string.IsNullOrWhiteSpace(date) && !FlexibleParser.TryParseDate(date, out day))
            throw new UserInputException("Bot chưa đọc được ngày này.");
        await DeferAsync(ephemeral: true);

        var key = day.ToString("yyyy-MM-dd");
        var partner = Owners.PartnerOf(Me.DiscordId);
        var mine = await Store.GetAsync<DailyAnswer>(StoreKinds.DailyAnswer, Key(key, Me), Ct);
        var theirs = partner == null ? null : await Store.GetAsync<DailyAnswer>(StoreKinds.DailyAnswer, Key(key, partner), Ct);

        if (mine == null && theirs == null)
        {
            await FollowupAsync(embed: Embeds.Plain("Chưa có câu trả lời nào cho ngày này cả."), ephemeral: true);
            return;
        }
        // The partner's answer is only visible once you've answered too, or the day is over.
        var showTheirs = theirs != null && (mine != null || day < today);
        var first = mine ?? theirs!;
        var embed = RevealEmbed(day, first, showTheirs && mine != null ? theirs : null,
            hiddenNote: theirs != null && !showTheirs ? "Người ấy đã trả lời rồi nè. Hãy gửi câu trả lời của bạn để cùng xem nhé!" : null);
        await FollowupAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("history", "Những câu trả lời gần đây của hai đứa")]
    public async Task HistoryAsync()
    {
        await DeferAsync(ephemeral: true);
        var today = Clock.Today();
        var all = (await Store.ListAsync<DailyAnswer>(StoreKinds.DailyAnswer, limit: 200, descending: true, ct: Ct)).Select(i => i.Value);
        var days = all.GroupBy(a => a.Date)
            .Where(g => g.Select(a => a.DiscordId).Distinct().Count() >= 2 || string.CompareOrdinal(g.Key, today.ToString("yyyy-MM-dd")) < 0)
            .OrderByDescending(g => g.Key).Take(5).ToList();
        if (days.Count == 0)
        {
            await FollowupAsync(embed: Embeds.Plain("Chưa có câu trả lời chung nào. Hãy bắt đầu với `/question today` nha."), ephemeral: true);
            return;
        }

        var eb = Embeds.Base(Palette.Pink).WithTitle("Những câu trả lời của tụi mình");
        foreach (var g in days)
        {
            var question = DateOnly.TryParse(g.Key, out var d) ? QuestionBank.ForDate(d) : "Câu hỏi";
            var text = string.Join("\n", g.OrderBy(a => a.Name).Select(a => $"**{a.Name}:** {Embeds.Truncate(a.Answer, 160)}"));
            eb.AddField(Embeds.Truncate($"{g.Key} · {question}", 250), Embeds.Truncate(text, 1000));
        }
        await FollowupAsync(embed: eb.Build(), ephemeral: true);
    }

    private DateOnly ValidateDate(string date)
    {
        var today = Clock.Today();
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day) || day > today || day < today.AddDays(-7))
            throw new UserInputException("Câu hỏi này đã qua quá lâu rồi. Hãy trả lời câu hỏi mới với `/question today` nha.");
        return day;
    }

    private static Embed RevealEmbed(DateOnly day, DailyAnswer a, DailyAnswer? b, string? hiddenNote = null)
    {
        var eb = Embeds.Base(Palette.Pink)
            .WithTitle("Câu trả lời của hai đứa")
            .WithDescription($"**{QuestionBank.ForDate(day)}**")
            .WithFooter(Embeds.FormatDate(day));
        foreach (var answer in new[] { a, b }.Where(x => x != null).OrderBy(x => x!.Name))
            eb.AddField(answer!.Name, Embeds.Truncate(answer.Answer, 1000));
        if (hiddenNote != null) eb.AddField("…", hiddenNote);
        return eb.Build();
    }
}
