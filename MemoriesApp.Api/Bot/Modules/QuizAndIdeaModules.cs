using System.Security.Cryptography;
using BinVaBun.DiscordBot.Content;
using BinVaBun.DiscordBot.Hosting;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;
using Discord.Interactions;

namespace BinVaBun.DiscordBot.Modules;

public sealed class QuizGuessModal : IModal
{
    public string Title => "Dự đoán của bạn";

    [InputLabel("Khoảnh khắc này diễn ra khi nào?")]
    [ModalTextInput("guess", TextInputStyle.Short, "yyyy-MM-dd hoặc dd/MM/yyyy", minLength: 6, maxLength: 20)]
    public string Guess { get; set; } = string.Empty;
}

public static class QuizScoring
{
    /// <summary>3 điểm cho ngày chuẩn xác, 2 điểm trong vòng 1 tuần, 1 điểm trong vòng 1 tháng.</summary>
    public static (int Points, bool Exact) ScoreDate(DateOnly guess, DateOnly actual)
    {
        var diff = Math.Abs(guess.DayNumber - actual.DayNumber);
        return diff == 0 ? (3, true) : diff <= 7 ? (2, false) : diff <= 31 ? (1, false) : (0, false);
    }
}

[Group("quiz", "Trò chơi thử thách: Ai nhớ kỷ niệm rõ hơn?")]
[RequireCouple]
[CommandContextType(InteractionContextType.Guild)]
public sealed class QuizModule : CompanionModuleBase
{
    public BotStore Store { get; set; } = null!;

    public enum Kind
    {
        [ChoiceDisplay("Đoán ngày diễn ra")] Date,
        [ChoiceDisplay("Đoán tên kỷ niệm")] Title
    }

    [SlashCommand("memory", "Một vòng thi đố vui về kỷ niệm của hai đứa")]
    public async Task StartAsync([Summary("type", "Bạn muốn đố kiểu nào?")] Kind kind = Kind.Date)
    {
        await DeferAsync();
        var memory = await Api.GetRandomAsync(Me.Username, false, null, Ct)
                     ?? throw new UserInputException("Bot cần có kỷ niệm để tạo câu đố nè. Hai đứa lưu thêm vài kỷ niệm với `/memory save` nha.");

        var type = kind == Kind.Title ? QuizType.Title : QuizType.Date;
        var options = new List<QuizOption>();
        string? fallbackNote = null;

        if (type == QuizType.Title)
        {
            var others = (await Api.ListAsync(Me.Username, new MemoryQuery(PageSize: 100), Ct)).Items
                .Where(m => m.Id != memory.Id && !m.Title.Equals(memory.Title, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(m => m.Title.ToLowerInvariant()).ToList();
            var hasClue = !memory.ContentIsTitle || memory.FirstImage != null;
            if (others.Count < 3 || !hasClue)
            {
                type = QuizType.Date;
                fallbackNote = "Chưa có đủ dữ liệu để chơi trò đoán tên, nên bot chuyển sang đoán ngày nha.";
            }
            else
            {
                options = others.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).Take(3)
                    .Append(memory).OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))
                    .Select(m => new QuizOption { MemoryId = m.Id, Label = Embeds.Truncate(m.Title, 100) }).ToList();
            }
        }

        var round = new QuizRound
        {
            Id = Guid.NewGuid().ToString("N")[..10],
            MemoryId = memory.Id,
            Type = type,
            Options = options,
            CreatedBy = Context.User.Id,
            CreatedAt = Clock.UtcNow
        };
        await Store.UpsertAsync(StoreKinds.QuizRound, round.Id, round, Ct);

        var eb = Embeds.Base(Palette.Pink).WithTitle("Thử thách kỷ niệm");
        if (type == QuizType.Date)
        {
            eb.WithDescription(string.Join("\n\n", new[]
            {
                fallbackNote is null ? null : $"*{fallbackNote}*",
                $"**{memory.Title}**",
                memory.ContentIsTitle ? null : Embeds.Truncate(memory.Content, 600),
                "**Khoảnh khắc này diễn ra khi nào nhỉ?**"
            }.Where(s => s != null)));
        }
        else
        {
            eb.WithDescription(string.Join("\n\n", new[]
            {
                memory.ContentIsTitle ? null : Embeds.Truncate(memory.Content, 600),
                $"Chuyện này diễn ra vào **{Embeds.FormatDate(memory.DateValue)}**.",
                "**Đây là kỷ niệm nào nhỉ?**"
            }.Where(s => s != null)));
        }
        if (memory.FirstImage is { } img) eb.WithImageUrl(img);
        eb.WithFooter("Mỗi người có một lượt đoán. Đáp án sẽ hiển thị sau khi cả hai đã đoán xong nha.");

        var components = new ComponentBuilder();
        if (type == QuizType.Date)
        {
            components.WithButton("Đoán ngay", $"quiz:guess:{round.Id}", ButtonStyle.Primary, row: 0);
        }
        else
        {
            var menu = new SelectMenuBuilder().WithCustomId($"quiz:pick:{round.Id}").WithPlaceholder("Chọn một kỷ niệm…").WithMinValues(1).WithMaxValues(1);
            foreach (var o in options) menu.AddOption(o.Label, o.MemoryId);
            components.WithSelectMenu(menu, row: 0);
        }
        components.WithButton("Xem đáp án ngay", $"quiz:reveal:{round.Id}", ButtonStyle.Secondary, row: 1);
        await FollowupAsync(embed: eb.Build(), components: components.Build());
    }

    [ComponentInteraction("quiz:guess:*", ignoreGroupNames: true)]
    public async Task OpenGuessAsync(string roundId)
    {
        if (await Store.ExistsAsync(StoreKinds.QuizReveal, roundId, Ct))
            throw new UserInputException("Vòng này đã mở đáp án rồi nha.");
        await RespondWithModalAsync<QuizGuessModal>($"quiz:modal:{roundId}");
    }

    [ModalInteraction("quiz:modal:*")]
    public async Task SubmitGuessAsync(string roundId, QuizGuessModal modal)
    {
        if (!FlexibleParser.TryParseDate(modal.Guess, out var guess))
            throw new UserInputException("Bot chưa đọc được ngày này. Bạn thử định dạng 2025-08-14 hoặc 14/08/2025 nha.");
        await DeferAsync(ephemeral: true);

        var round = await LoadRoundAsync(roundId);
        var memory = await LoadMemoryAsync(round);
        var (points, exact) = QuizScoring.ScoreDate(guess, memory.DateValue);
        await RecordAsync(round, guess.ToString("yyyy-MM-dd"), points, exact);
    }

    [ComponentInteraction("quiz:pick:*", ignoreGroupNames: true)]
    public async Task SubmitPickAsync(string roundId, string[] selected)
    {
        await DeferAsync();
        var round = await LoadRoundAsync(roundId);
        var pick = selected.FirstOrDefault() ?? string.Empty;
        var correct = pick == round.MemoryId;
        await RecordAsync(round, round.Options.FirstOrDefault(o => o.MemoryId == pick)?.Label ?? "?", correct ? 2 : 0, correct);
    }

    [ComponentInteraction("quiz:reveal:*", ignoreGroupNames: true)]
    public async Task RevealAsync(string roundId)
    {
        await DeferAsync();
        var round = await LoadRoundAsync(roundId);
        if (!await TryRevealAsync(round))
            await FollowupAsync(embed: Embeds.Plain("Đã mở đáp án ở trên rồi nha."), ephemeral: true);
    }

    [SlashCommand("scoreboard", "Bảng điểm xem ai nhớ kỷ niệm giỏi hơn")]
    public async Task ScoreboardAsync()
    {
        await DeferAsync();
        var answers = (await Store.ListAsync<QuizAnswer>(StoreKinds.QuizAnswer, limit: 500, ct: Ct)).Select(i => i.Value).ToList();
        if (answers.Count == 0)
        {
            await FollowupAsync(embed: Embeds.Plain("Chưa có vòng đố nào. Bắt đầu với `/quiz memory` nha."));
            return;
        }

        var eb = Embeds.Base(Palette.Gold).WithTitle("Ai nhớ kỷ niệm giỏi hơn?");
        var rows = Owners.All.Select(o =>
        {
            var mine = answers.Where(a => a.DiscordId == o.DiscordId).ToList();
            return (Owner: o, Points: mine.Sum(a => a.Points), Rounds: mine.Count, Exact: mine.Count(a => a.Exact));
        }).OrderByDescending(r => r.Points).ToList();
        foreach (var r in rows)
            eb.AddField(r.Owner.Name, $"**{r.Points}** điểm\n{r.Rounds} lượt đoán · {r.Exact} lần chuẩn xác", inline: true);
        eb.WithFooter(rows.Count == 2 && rows[0].Points == rows[1].Points ? "Hai đứa đang hòa điểm nhau nè!" : $"{rows[0].Owner.Name} đang dẫn trước rồi đó nha!");
        await FollowupAsync(embed: eb.Build());
    }

    // ── helpers ──

    private async Task RecordAsync(QuizRound round, string guess, int points, bool exact)
    {
        if (await Store.ExistsAsync(StoreKinds.QuizReveal, round.Id, Ct))
        {
            await FollowupAsync(embed: Embeds.Plain("Vòng này đã mở đáp án rồi nha."), ephemeral: true);
            return;
        }

        var answer = new QuizAnswer { RoundId = round.Id, DiscordId = Context.User.Id, Guess = guess, Points = points, Exact = exact };
        if (!await Store.TryClaimAsync(StoreKinds.QuizAnswer, $"{round.Id}:{Me.KeyToken}", answer, Ct))
        {
            await FollowupAsync(embed: Embeds.Plain("Bạn đã chốt dự đoán cho vòng này rồi nè."), ephemeral: true);
            return;
        }

        var partner = Owners.PartnerOf(Me.DiscordId);
        var theirs = partner == null ? null : await Store.GetAsync<QuizAnswer>(StoreKinds.QuizAnswer, $"{round.Id}:{partner.KeyToken}", Ct);
        if (theirs == null)
        {
            await FollowupAsync(embed: Embeds.Plain("Đã ghi nhận! Bot sẽ mở kết quả ngay khi cả hai đứa đều đoán xong nha."), ephemeral: true);
            return;
        }
        await FollowupAsync(embed: Embeds.Plain("Đã ghi nhận! Cả hai đứa đều đã đoán xong — cùng xem đáp án nào!"), ephemeral: true);
        await TryRevealAsync(round);
    }

    private async Task<bool> TryRevealAsync(QuizRound round)
    {
        if (!await Store.TryClaimAsync(StoreKinds.QuizReveal, round.Id, new { at = Clock.UtcNow }, Ct)) return false;

        var memory = await LoadMemoryAsync(round);
        var eb = Embeds.Base(Palette.Gold).WithTitle("Đáp án chính xác")
            .WithDescription($"**{memory.Title}**\n{Embeds.FormatDate(memory.DateValue)}");
        if (memory.FirstImage is { } img) eb.WithThumbnailUrl(img);

        foreach (var o in Owners.All)
        {
            var a = await Store.GetAsync<QuizAnswer>(StoreKinds.QuizAnswer, $"{round.Id}:{o.KeyToken}", Ct);
            eb.AddField(o.Name, a == null ? "Chưa đoán" : $"{a.Guess}\n**+{a.Points}**{(a.Exact ? " · chuẩn xác!" : string.Empty)}", inline: true);
        }
        await Context.Channel.SendMessageAsync(embed: eb.Build());
        return true;
    }

    private async Task<QuizRound> LoadRoundAsync(string roundId) =>
        await Store.GetAsync<QuizRound>(StoreKinds.QuizRound, roundId, Ct)
        ?? throw new UserInputException("Bot không tìm thấy vòng chơi này nữa rồi.");

    private async Task<Memory> LoadMemoryAsync(QuizRound round) =>
        await Api.GetAsync(Me.Username, round.MemoryId, Ct)
        ?? throw new UserInputException("Kỷ niệm này không còn trong hộp thời gian nữa.");
}

[Group("dateidea", "Gợi ý những hoạt động hẹn hò thú vị cho hai đứa")]
[RequireCouple]
[CommandContextType(InteractionContextType.Guild)]
public sealed class DateIdeaModule : CompanionModuleBase
{
    [SlashCommand("random", "Một ý tưởng hẹn hò đơn giản ngọt ngào cho hai đứa")]
    public async Task RandomAsync([Summary("mood", "Hôm nay hai đứa có tâm trạng thế nào?")] DateMood mood = DateMood.Any)
    {
        await RespondAsync(embed: Card(mood), components: Again(mood));
    }

    [ComponentInteraction("dateidea:again:*", ignoreGroupNames: true)]
    public async Task AgainAsync(string mood)
    {
        var parsed = Enum.TryParse<DateMood>(mood, out var m) && Enum.IsDefined(m) ? m : DateMood.Any;
        await DeferAsync();
        await ModifyOriginalResponseAsync(p => { p.Embed = Card(parsed); p.Components = Again(parsed); });
    }

    private static Embed Card(DateMood mood) =>
        Embeds.Base(Palette.Cream).WithTitle("Ý tưởng hẹn hò cho hai đứa").WithDescription(DateIdeas.Random(mood)).Build();

    private static MessageComponent Again(DateMood mood) =>
        new ComponentBuilder().WithButton("Gợi ý khác", $"dateidea:again:{mood}", ButtonStyle.Secondary).Build();
}
