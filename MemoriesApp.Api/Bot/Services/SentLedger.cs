using BinVaBun.DiscordBot.Configuration;
using BinVaBun.DiscordBot.Models;
using Microsoft.Extensions.Options;

namespace BinVaBun.DiscordBot.Services;

/// <summary>
/// Exactly-once guard for scheduled messages. A claim is atomically created in the API store
/// before sending; if sending fails the claim is released so the next tick can retry.
/// Because the ledger lives in the database, restarts and even two bot instances can't double-post.
/// </summary>
public sealed class SentLedger(BotStore store, IClock clock)
{
    public async Task<bool> RunOnceAsync(string key, Func<Task> send, string? memoryId = null, CancellationToken ct = default)
    {
        var marker = new SentMarker { MemoryId = memoryId, At = clock.UtcNow };
        if (!await store.TryClaimAsync(StoreKinds.Sent, key, marker, ct)) return false;
        try
        {
            await send();
            return true;
        }
        catch
        {
            try { await store.DeleteAsync(StoreKinds.Sent, key, CancellationToken.None); } catch { /* retry next tick is best effort */ }
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> RecentMemoryIdsAsync(string prefix, int count, CancellationToken ct)
    {
        var items = await store.ListAsync<SentMarker>(StoreKinds.Sent, prefix, count, descending: true, ct);
        return items.Select(i => i.Value.MemoryId).Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToList();
    }
}

/// <summary>Persisted schedule configuration (channel, times, toggles), with env-var defaults.</summary>
public sealed class ScheduleSettingsService(BotStore store, IOptions<CompanionOptions> companion, IOptions<DiscordOptions> discord, IClock clock)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);
    private ScheduleSettings? _cached;
    private DateTimeOffset _cachedAt;

    public sealed record Effective(ulong? ChannelId, TimeOnly DailyMemory, TimeOnly DailyQuestion, TimeOnly Anniversary,
        bool DailyMemoryEnabled, bool DailyQuestionEnabled);

    public async Task<ScheduleSettings> GetStoredAsync(CancellationToken ct, bool forceRefresh = false)
    {
        if (!forceRefresh && _cached != null && clock.UtcNow - _cachedAt < CacheFor) return _cached;
        _cached = await store.GetAsync<ScheduleSettings>(StoreKinds.Settings, "schedule", ct) ?? new ScheduleSettings();
        _cachedAt = clock.UtcNow;
        return _cached;
    }

    public async Task SaveAsync(ScheduleSettings settings, CancellationToken ct)
    {
        await store.UpsertAsync(StoreKinds.Settings, "schedule", settings, ct);
        _cached = settings;
        _cachedAt = clock.UtcNow;
    }

    public async Task<Effective> GetEffectiveAsync(CancellationToken ct)
    {
        var s = await GetStoredAsync(ct);
        var o = companion.Value;
        return new Effective(
            s.ChannelId ?? discord.Value.DefaultChannelId,
            ParseOr(s.DailyMemoryTime, o.DailyMemoryTime, new TimeOnly(20, 0)),
            ParseOr(s.DailyQuestionTime, o.DailyQuestionTime, new TimeOnly(9, 0)),
            ParseOr(s.AnniversaryTime, o.AnniversaryTime, new TimeOnly(8, 0)),
            s.DailyMemoryEnabled, s.DailyQuestionEnabled);
    }

    private static TimeOnly ParseOr(string? stored, string configured, TimeOnly fallback)
    {
        if (FlexibleParser.TryParseTimeOfDay(stored, out var t)) return t;
        return FlexibleParser.TryParseTimeOfDay(configured, out t) ? t : fallback;
    }
}

