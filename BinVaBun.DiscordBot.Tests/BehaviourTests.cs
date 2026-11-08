using System.Net;
using BinVaBun.DiscordBot.Configuration;
using BinVaBun.DiscordBot.Hosting;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Modules;
using BinVaBun.DiscordBot.BackgroundServices;
using BinVaBun.DiscordBot.Extensions;
using Microsoft.Extensions.Configuration;
using BinVaBun.DiscordBot.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BinVaBun.DiscordBot.Tests;

public class ApiTransportTests
{
    private static (ApiTransport Transport, FakeApiHandler Handler) Make()
    {
        var handler = new FakeApiHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") };
        var transport = new ApiTransport(http, NullLogger<ApiTransport>.Instance)
        {
            RetryDelays = [TimeSpan.Zero, TimeSpan.Zero]
        };
        return (transport, handler);
    }

    [Fact]
    public async Task Get_retries_through_a_cold_start_and_then_succeeds()
    {
        var (t, h) = Make();
        h.FailNext = 2; // e.g. Render waking up
        h.Records["settings|schedule"] = "{}";
        using var r = await t.SendAsync("u", HttpMethod.Get, "api/bot/store/settings/schedule", null, default);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(3, h.Requests.Count);
    }

    [Fact]
    public async Task Get_gives_up_with_a_friendly_error_when_the_api_stays_down()
    {
        var (t, h) = Make();
        h.FailNext = 10;
        var ex = await Assert.ThrowsAsync<ApiUnavailableException>(() =>
            t.SendAsync("u", HttpMethod.Get, "api/bot/me", null, default));
        Assert.DoesNotContain("503", ex.UserMessage);
        Assert.Equal(3, h.Requests.Count);
    }

    [Fact]
    public async Task Post_is_never_retried_so_a_memory_cannot_be_saved_twice()
    {
        var (t, h) = Make();
        h.FailNext = 1;
        await Assert.ThrowsAsync<ApiUnavailableException>(() =>
            t.SendAsync("u", HttpMethod.Post, "api/bot/memories", () => new StringContent("x"), default));
        Assert.Single(h.Requests);
    }

    [Fact]
    public async Task Network_errors_are_translated()
    {
        var (t, h) = Make();
        h.Override = _ => throw new HttpRequestException("connection refused: secret-host:1234");
        var ex = await Assert.ThrowsAsync<ApiUnavailableException>(() => t.SendAsync("u", HttpMethod.Get, "api/bot/me", null, default));
        Assert.DoesNotContain("secret-host", ex.UserMessage);
    }

    [Fact]
    public async Task Auth_failures_do_not_leak_details_and_are_not_retried()
    {
        var (t, h) = Make();
        h.Override = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);
        using var r = await t.SendAsync("u", HttpMethod.Get, "api/bot/me", null, default);
        await Assert.ThrowsAsync<ApiAuthException>(() => ApiTransport.EnsureSuccessAsync(r, default));
        Assert.Single(h.Requests);
    }

    [Fact]
    public async Task Curated_api_errors_are_shown_but_oversized_ones_are_not()
    {
        var (t, h) = Make();
        h.Override = _ => FakeApiHandler.Json(HttpStatusCode.BadRequest, new { error = "That date hasn't happened yet." });
        using var ok = await t.SendAsync("u", HttpMethod.Get, "x", null, default);
        var ex = await Assert.ThrowsAsync<ApiRejectedException>(() => ApiTransport.EnsureSuccessAsync(ok, default));
        Assert.Equal("That date hasn't happened yet.", ex.UserMessage);

        h.Override = _ => FakeApiHandler.Json(HttpStatusCode.BadRequest, new { error = new string('x', 5000) });
        using var big = await t.SendAsync("u", HttpMethod.Get, "x", null, default);
        var ex2 = await Assert.ThrowsAsync<ApiRejectedException>(() => ApiTransport.EnsureSuccessAsync(big, default));
        Assert.True(ex2.UserMessage.Length < 200);
    }

    [Fact]
    public async Task Every_request_carries_the_acting_user()
    {
        var (t, h) = Make();
        string? seen = null;
        h.Override = req => { seen = req.Headers.GetValues("X-Bot-User").Single(); return new HttpResponseMessage(HttpStatusCode.OK); };
        using var _ = await t.SendAsync("Hlinh", HttpMethod.Get, "api/bot/me", null, default);
        Assert.Equal("Hlinh", seen);
    }
}

public class SchedulerDuplicationTests
{
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 8, 13, 0, 0, TimeSpan.Zero);
        public TimeZoneInfo Zone => TimeZones.Resolve("Asia/Ho_Chi_Minh");
    }

    private static (SentLedger Ledger, BotStore Store, FakeApiHandler Handler) Make()
    {
        var handler = new FakeApiHandler();
        var transport = new ApiTransport(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") }, NullLogger<ApiTransport>.Instance)
        { RetryDelays = [TimeSpan.Zero] };
        var owners = new OwnerDirectory(new[] { new OwnerOptions { DiscordId = 1, Username = "Hlinh" } });
        var store = new BotStore(transport, owners);
        return (new SentLedger(store, new Clock()), store, handler);
    }

    [Fact]
    public async Task A_daily_post_is_sent_once_even_if_the_tick_runs_many_times()
    {
        var (ledger, _, _) = Make();
        var sends = 0;
        for (var i = 0; i < 5; i++)
            await ledger.RunOnceAsync("dailymemory:2026-11-08", () => { sends++; return Task.CompletedTask; });
        Assert.Equal(1, sends);
    }

    [Fact]
    public async Task Concurrent_instances_cannot_both_send()
    {
        var (ledger, _, _) = Make();
        var sends = 0;
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            ledger.RunOnceAsync("anniv:abc:2026-12-24:7", () => { Interlocked.Increment(ref sends); return Task.CompletedTask; })));
        Assert.Equal(1, sends);
        Assert.Single(results, r => r);
    }

    [Fact]
    public async Task A_restart_still_remembers_what_was_sent()
    {
        var (ledger, store, handler) = Make();
        await ledger.RunOnceAsync("dailyquestion:2026-11-08", () => Task.CompletedTask);

        // "Restart": brand-new ledger instance over the same persisted records.
        var afterRestart = new SentLedger(store, new Clock());
        var sent = await afterRestart.RunOnceAsync("dailyquestion:2026-11-08", () => throw new Exception("must not send again"));
        Assert.False(sent);
        Assert.Contains("sent|dailyquestion:2026-11-08", handler.Records.Keys);
    }

    [Fact]
    public async Task A_failed_send_releases_the_claim_so_the_next_tick_retries()
    {
        var (ledger, _, _) = Make();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ledger.RunOnceAsync("reminder:r1", () => throw new InvalidOperationException("discord down")));

        var delivered = false;
        var sent = await ledger.RunOnceAsync("reminder:r1", () => { delivered = true; return Task.CompletedTask; });
        Assert.True(sent);
        Assert.True(delivered);
    }

    [Fact]
    public async Task Store_round_trips_typed_records()
    {
        var (_, store, _) = Make();
        var a = new Anniversary { Id = "a1", Title = "First date", Date = new DateOnly(2024, 2, 14), Kind = AnniversaryKind.FirstDate, Yearly = true, CreatedBy = 1 };
        await store.UpsertAsync(StoreKinds.Anniversary, a.Id, a, default);
        var back = await store.GetAsync<Anniversary>(StoreKinds.Anniversary, "a1", default);
        Assert.Equal(("First date", new DateOnly(2024, 2, 14), AnniversaryKind.FirstDate), (back!.Title, back.Date, back.Kind));
        Assert.True(await store.DeleteAsync(StoreKinds.Anniversary, "a1", default));
        Assert.Null(await store.GetAsync<Anniversary>(StoreKinds.Anniversary, "a1", default));
    }
}

public class AuthorizationTests
{
    private static (RequireCoupleAttribute Attr, IServiceProvider Services) Make()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new OwnerDirectory(new[]
        {
            new OwnerOptions { DiscordId = 111, Username = "Hlinh" },
            new OwnerOptions { DiscordId = 222, Username = "Pthao" }
        }));
        services.AddSingleton<IOptions<DiscordOptions>>(Options.Create(new DiscordOptions { GuildId = 999, Token = "t" }));
        return (new RequireCoupleAttribute(), services.BuildServiceProvider());
    }

    private static IInteractionContext Ctx(ulong userId, ulong? guildId)
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(userId);
        var ctx = Substitute.For<IInteractionContext>();
        ctx.User.Returns(user);
        if (guildId is { } g)
        {
            var guild = Substitute.For<IGuild>();
            guild.Id.Returns(g);
            ctx.Guild.Returns(guild);
        }
        return ctx;
    }

    [Theory]
    [InlineData(111UL, 999UL, true)]    // owner, right server
    [InlineData(222UL, 999UL, true)]
    [InlineData(333UL, 999UL, false)]   // in the server but not allowlisted
    [InlineData(111UL, 555UL, false)]   // owner, but a different server
    [InlineData(111UL, null, false)]    // owner, but DM
    public async Task Only_the_two_owners_in_the_private_server_get_through(ulong user, ulong? guild, bool allowed)
    {
        var (attr, services) = Make();
        var result = await attr.CheckRequirementsAsync(Ctx(user, guild), null!, services);
        Assert.Equal(allowed, result.IsSuccess);
    }

    [Fact]
    public void Every_module_is_protected_by_the_allowlist_gate()
    {
        var modules = typeof(CompanionModuleBase).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IInteractionModuleBase).IsAssignableFrom(t)).ToList();
        Assert.NotEmpty(modules);
        foreach (var module in modules)
            Assert.True(module.GetCustomAttributes(typeof(RequireCoupleAttribute), inherit: false).Length == 1,
                $"{module.Name} is missing [RequireCouple]");
    }

    [Fact]
    public async Task All_modules_and_commands_are_valid_for_discord()
    {
        // Discord.Net validates names, option limits, enums, wildcards and handlers while building modules.
        var service = new InteractionService(new DiscordSocketClient());
        var modules = await service.AddModulesAsync(typeof(CompanionModuleBase).Assembly, new ServiceCollection().AddCompanion(new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Discord:Token"] = "token", ["Discord:GuildId"] = "999", ["Api:BaseUrl"] = "http://localhost/", ["Api:BotKey"] = "key", ["Owners:0:DiscordId"] = "1", ["Owners:0:Username"] = "u" }).Build()).BuildServiceProvider());
        Assert.Equal(9, modules.Count());
        var groups = service.Modules.Select(m => m.SlashGroupName).Where(n => n != null).ToHashSet();
        foreach (var name in new[] { "memory", "anniversary", "remind", "question", "quiz", "dateidea", "lovenote", "companion", "dating" })
            Assert.Contains(name, groups);
        Assert.NotEmpty(service.ComponentCommands);
        foreach (var customId in new[] { "mem:pick:1234567890", "mem:page:1234567890:2", "mem:again:0", "mem:img:123:0:n", "question:answer:2026-11-08", "quiz:guess:123", "quiz:pick:123", "quiz:reveal:123", "dateidea:again:Romantic", "dating:nav:next:2026:12" })
        {
            var mockInteraction = Substitute.For<IComponentInteraction>();
            var mockData = Substitute.For<IComponentInteractionData>();
            mockData.CustomId.Returns(customId);
            mockInteraction.Data.Returns(mockData);
            var search = service.SearchComponentCommand(mockInteraction);
            Assert.True(search.IsSuccess, $"Search failed for '{customId}': {search.ErrorReason}");
        }
    }
}
