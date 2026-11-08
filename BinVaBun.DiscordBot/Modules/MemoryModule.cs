using BinVaBun.DiscordBot.Content;
using BinVaBun.DiscordBot.Hosting;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace BinVaBun.DiscordBot.Modules;

/// <summary>Builds paginated memory messages (list or photo gallery) with a select menu and prev/next buttons.</summary>
public static class MemoryPager
{
    public const int ListPageSize = 5;
    public const int GalleryPageSize = 8;

    public static int PageSizeFor(PagerState s) => s.Kind == PagerKind.Gallery ? GalleryPageSize : ListPageSize;

    public static MemoryQuery ToQuery(PagerState s, int page) =>
        new(s.Query, s.Tag, s.Year, s.Month, s.HasImage, s.Ascending, Math.Max(1, page), PageSizeFor(s));

    public static (Embed[] Embeds, MessageComponent Components) Build(PagerState state, MemoryPage page, string stateId)
    {
        Embed[] embeds;
        if (state.Kind == PagerKind.Gallery)
        {
            var head = Content.Embeds.Base().WithTitle(state.Heading)
                .WithFooter($"Trang {Math.Max(1, page.Page)} / {Math.Max(1, page.TotalPages)} · {page.TotalCount} ảnh").Build();
            embeds = new[] { head }.Concat(page.Items.Select(m =>
                Content.Embeds.Base(Palette.Cream)
                    .WithTitle(Content.Embeds.Truncate(m.Title, 200))
                    .WithDescription(Content.Embeds.FormatDate(m.DateValue))
                    .WithImageUrl(m.FirstImage).Build())).ToArray();
        }
        else
        {
            embeds = [Content.Embeds.MemoryListPage(state, page)];
        }

        var components = new ComponentBuilder();
        if (page.Items.Count > 0)
        {
            var menu = new SelectMenuBuilder().WithCustomId($"mem:pick:{stateId}").WithPlaceholder("Chọn xem một kỷ niệm…").WithMinValues(1).WithMaxValues(1);
            foreach (var m in page.Items)
                menu.AddOption(Content.Embeds.Truncate(m.Title, 100), m.Id, Content.Embeds.FormatDate(m.DateValue));
            components.WithSelectMenu(menu, row: 0);
        }
        components
            .WithButton("Trang trước", $"mem:page:{stateId}:{page.Page - 1}", ButtonStyle.Secondary, disabled: page.Page <= 1, row: 1)
            .WithButton("Trang sau", $"mem:page:{stateId}:{page.Page + 1}", ButtonStyle.Secondary, disabled: page.Page >= page.TotalPages, row: 1);
        return (embeds, components.Build());
    }
}

public sealed class MemoryAutocomplete : GuardedAutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, IAutocompleteInteraction interaction, IParameterInfo parameter, IServiceProvider services)
    {
        if (!IsAllowed(context, services, out var owner)) return AutocompletionResult.FromSuccess();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
            var text = interaction.Data.Current.Value?.ToString();
            var page = await services.GetRequiredService<MemoriesApiClient>()
                .ListAsync(owner.Username, new MemoryQuery(Query: text, PageSize: 20), cts.Token);
            return AutocompletionResult.FromSuccess(page.Items.Select(m =>
                new AutocompleteResult(Label($"{m.Title} · {m.DateValue:yyyy-MM-dd}"), m.Id)));
        }
        catch
        {
            return AutocompletionResult.FromSuccess(); // slow or unavailable API: just offer nothing
        }
    }
}

[Group("memory", "Hộp thời gian lưu giữ kỷ niệm của hai đứa")]
[RequireCouple]
[CommandContextType(InteractionContextType.Guild)]
public sealed class MemoryModule : CompanionModuleBase
{
    public ImageAttachmentService Images { get; set; } = null!;
    public PagerStateStore Pagers { get; set; } = null!;

    [SlashCommand("save", "Cất giữ một kỷ niệm mới vào hộp thời gian")]
    public async Task SaveAsync(
        [Summary("title", "Tên kỷ niệm là gì nè?"), MaxLength(100)] string title,
        [Summary("description", "Câu chuyện đằng sau khoảnh khắc này"), MaxLength(2000)] string? description = null,
        [Summary("date", "Ngày diễn ra, vd: 2026-12-24 hoặc 24/12/2026 (mặc định: hôm nay)")] string? date = null,
        [Summary("tags", "Các thẻ phân loại cách nhau bởi dấu phẩy, vd: henho, dulich, anan")] string? tags = null,
        [Summary("photo", "Ảnh đính kèm kỷ niệm")] IAttachment? photo = null)
    {
        await DeferAsync();

        DateOnly? parsedDate = null;
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!FlexibleParser.TryParseDate(date, out var d))
                throw new UserInputException("Bot chưa đọc được ngày này. Bạn thử định dạng 2026-12-24 hoặc 24/12/2026 nha.");
            parsedDate = d;
        }

        ImageUpload? upload = null;
        if (photo != null)
            upload = await Images.DownloadAsync(photo.Url, photo.Filename, photo.ContentType, photo.Size, Ct);

        var memory = await Api.CreateAsync(Me.Username, new CreateMemoryRequest(title.Trim(), description, parsedDate, tags, upload), Ct);
        var (embeds, comps) = Embeds.MemoryCards(memory, Voice.Pick(Voice.Saved), Palette.Pink);
        await FollowupAsync(embeds: embeds, components: comps);
    }

    [SlashCommand("random", "Mở ngẫu nhiên một kỷ niệm từ hộp thời gian")]
    public async Task RandomAsync([Summary("photo_only", "Chỉ chọn kỷ niệm có ảnh")] bool photoOnly = false)
    {
        await DeferAsync();
        var memory = await Api.GetRandomAsync(Me.Username, photoOnly, null, Ct);
        if (memory == null)
        {
            await FollowupAsync(embed: Embeds.Plain(Voice.Pick(Voice.NothingYet)));
            return;
        }
        var (embeds, comps) = Embeds.MemoryCards(memory, Voice.Pick(Voice.Found), includeAgainButton: true, againPhotoOnly: photoOnly);
        await FollowupAsync(embeds: embeds, components: comps);
    }

    [SlashCommand("photo", "Nhắc lại một bức ảnh cũ ngẫu nhiên")]
    public Task PhotoAsync() => RandomAsync(photoOnly: true);

    [ComponentInteraction("mem:again:*", ignoreGroupNames: true)]
    public async Task AgainAsync(string photoOnly)
    {
        await DeferAsync();
        var photo = photoOnly == "1";
        var memory = await Api.GetRandomAsync(Me.Username, photo, null, Ct);
        if (memory == null) return;
        var (embeds, comps) = Embeds.MemoryCards(memory, Voice.Pick(Voice.Found), includeAgainButton: true, againPhotoOnly: photo);
        await ModifyOriginalResponseAsync(m =>
        {
            m.Embeds = embeds;
            m.Components = comps;
        });
    }

    [SlashCommand("latest", "Những kỷ niệm mới nhất của tụi mình")]
    public async Task LatestAsync()
    {
        await DeferAsync();
        await ShowPagedAsync(new PagerState(PagerKind.List, "Những kỷ niệm mới nhất"));
    }

    [SlashCommand("search", "Tìm kiếm kỷ niệm theo từ khóa hoặc thẻ")]
    public async Task SearchAsync(
        [Summary("words", "Từ khóa cần tìm (không dấu hoặc có dấu đều được)")] string? words = null,
        [Summary("tag", "Tên thẻ, vd: henho, dulich")] string? tag = null)
    {
        if (string.IsNullOrWhiteSpace(words) && string.IsNullOrWhiteSpace(tag))
            throw new UserInputException("Hãy nhập vài từ khóa hoặc tên thẻ để bot tìm nha.");
        await DeferAsync();

        var what = string.Join(" · ", new[]
        {
            string.IsNullOrWhiteSpace(words) ? null : $"“{Embeds.Truncate(words.Trim(), 60)}”",
            string.IsNullOrWhiteSpace(tag) ? null : $"#{tag.Trim().TrimStart('#')}"
        }.Where(s => s != null));
        await ShowPagedAsync(new PagerState(PagerKind.List, $"Kỷ niệm khớp với {what}", words?.Trim(), tag?.Trim()));
    }

    [SlashCommand("timeline", "Dòng thời gian câu chuyện của tụi mình")]
    public async Task TimelineAsync(
        [Summary("year", "Năm (vd: 2026)"), MinValue(1900), MaxValue(2100)] int? year = null,
        [Summary("month", "Tháng (1-12)"), MinValue(1), MaxValue(12)] int? month = null)
    {
        await DeferAsync();
        if (month != null && year == null) year = Clock.Today().Year;
        var heading = (year, month) switch
        {
            ({ } y, { } m) => $"Dòng thời gian · Tháng {m:D2}/{y}",
            ({ } y, null) => $"Dòng thời gian · Năm {y}",
            _ => "Dòng thời gian của tụi mình"
        };
        await ShowPagedAsync(new PagerState(PagerKind.List, heading, Year: year, Month: month, Ascending: true));
    }

    [SlashCommand("gallery", "Bộ sưu tập ảnh kỷ niệm theo tháng hoặc năm")]
    public async Task GalleryAsync(
        [Summary("year", "Năm (vd: 2026)"), MinValue(1900), MaxValue(2100)] int? year = null,
        [Summary("month", "Tháng (1-12)"), MinValue(1), MaxValue(12)] int? month = null)
    {
        await DeferAsync();
        if (month != null && year == null) year = Clock.Today().Year;
        var when = (year, month) switch
        {
            ({ } y, { } m) => $" · Tháng {m:D2}/{y}",
            ({ } y, null) => $" · Năm {y}",
            _ => string.Empty
        };
        await ShowPagedAsync(new PagerState(PagerKind.Gallery, "Bộ sưu tập ảnh" + when, Year: year, Month: month, Ascending: true, HasImage: true));
    }

    [SlashCommand("today", "Kỷ niệm vào ngày này của những năm trước")]
    public async Task TodayAsync()
    {
        await DeferAsync();
        var items = await Api.OnThisDayAsync(Me.Username, Clock.Today(), Ct);
        if (items.Count == 0)
        {
            await FollowupAsync(embed: Embeds.Plain("Hộp thời gian chưa có kỷ niệm nào của ngày này ở các năm trước. Hôm nay hãy cùng nhau tạo nên một kỷ niệm mới nhé!"));
            return;
        }
        var lead = Voice.Pick(Voice.OnThisDay);
        var embeds = items.Take(5).Select((m, i) => Embeds.MemoryCard(m, i == 0 ? lead : null)).ToArray();
        await FollowupAsync(embeds: embeds);
    }

    [SlashCommand("detail", "Xem chi tiết một kỷ niệm")]
    public async Task DetailAsync(
        [Summary("memory", "Gõ chữ để tìm và chọn một kỷ niệm"), Autocomplete(typeof(MemoryAutocomplete))] string memory)
    {
        await DeferAsync();
        Memory? found;
        if (MemoriesApiClient.IsValidId(memory))
        {
            found = await Api.GetAsync(Me.Username, memory, Ct);
        }
        else
        {
            // Typed text instead of a picked suggestion: open the best match.
            var page = await Api.ListAsync(Me.Username, new MemoryQuery(Query: memory, PageSize: 1), Ct);
            found = page.Items.FirstOrDefault();
        }

        if (found == null)
            await FollowupAsync(embed: Embeds.Plain(Voice.Pick(Voice.NoMatches)));
        else
        {
            var (embeds, comps) = Embeds.MemoryCards(found);
            await FollowupAsync(embeds: embeds, components: comps);
        }
    }

    [ComponentInteraction("mem:page:*:*", ignoreGroupNames: true)]
    public async Task PageAsync(string stateId, string page)
    {
        await DeferAsync();
        if (!int.TryParse(page, out var number) || !Pagers.TryGet(stateId, out var state))
        {
            await FollowupAsync(embed: Embeds.Plain(Voice.Expired), ephemeral: true);
            return;
        }
        var result = await Api.ListAsync(Me.Username, MemoryPager.ToQuery(state, number), Ct);
        var (embeds, components) = MemoryPager.Build(state, result, stateId);
        await ModifyOriginalResponseAsync(m => { m.Embeds = embeds; m.Components = components; });
    }

    [ComponentInteraction("mem:pick:*", ignoreGroupNames: true)]
    public async Task PickAsync(string stateId, string[] selected)
    {
        await DeferAsync();
        var id = selected.FirstOrDefault();
        var memory = id == null ? null : await Api.GetAsync(Me.Username, id, Ct);
        if (memory == null)
            await FollowupAsync(embed: Embeds.Plain("Bot không tìm thấy kỷ niệm này nữa rồi."), ephemeral: true);
        else
        {
            var (embeds, comps) = Embeds.MemoryCards(memory);
            await FollowupAsync(embeds: embeds, components: comps, ephemeral: true);
        }
    }

    [ComponentInteraction("mem:img:*:*:*", ignoreGroupNames: true)]
    public async Task PhotoPageAsync(string memoryId, string pageStr, string againFlag)
    {
        await DeferAsync();
        if (!int.TryParse(pageStr, out var page)) return;
        var memory = await Api.GetAsync(Me.Username, memoryId, Ct);
        if (memory == null) return;
        var includeAgain = againFlag is "1" or "0";
        var photoOnly = againFlag == "1";
        var (embeds, components) = Embeds.MemoryCards(memory, photoPage: page, includeAgainButton: includeAgain, againPhotoOnly: photoOnly);
        await ModifyOriginalResponseAsync(m =>
        {
            m.Embeds = embeds;
            m.Components = components;
        });
    }

    [ComponentInteraction("mem:noop", ignoreGroupNames: true)]
    public Task NoOpAsync() => Task.CompletedTask;

    private async Task ShowPagedAsync(PagerState state)
    {
        var first = await Api.ListAsync(Me.Username, MemoryPager.ToQuery(state, 1), Ct);
        if (first.TotalCount == 0)
        {
            var text = state.Query == null && state.Tag == null && state.Year == null && state.Month == null
                ? Voice.Pick(Voice.NothingYet)
                : Voice.Pick(Voice.NoMatches);
            await FollowupAsync(embed: Embeds.Plain(text));
            return;
        }
        var stateId = Pagers.Save(state);
        var (embeds, components) = MemoryPager.Build(state, first, stateId);
        await FollowupAsync(embeds: embeds, components: components);
    }

    private static MessageComponent AgainButton(bool photoOnly) =>
        new ComponentBuilder().WithButton("Xem kỷ niệm khác", $"mem:again:{(photoOnly ? 1 : 0)}", ButtonStyle.Secondary).Build();
}

