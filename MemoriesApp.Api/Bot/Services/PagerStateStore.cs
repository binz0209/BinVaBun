using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace BinVaBun.DiscordBot.Services;

public enum PagerKind { List, Gallery }

/// <summary>What a paginated message is showing, so button presses can fetch the next page.</summary>
public sealed record PagerState(
    PagerKind Kind, string Heading, string? Query = null, string? Tag = null,
    int? Year = null, int? Month = null, bool Ascending = false, bool HasImage = false);

/// <summary>
/// Keeps pager state server-side (Discord custom ids are limited to 100 chars).
/// State is in memory only: after a restart old buttons politely ask to re-run the command.
/// </summary>
public sealed class PagerStateStore(IClock clock)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(2);
    private readonly ConcurrentDictionary<string, (PagerState State, DateTimeOffset Expires)> _states = new();

    public string Save(PagerState state)
    {
        Prune();
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();
        _states[id] = (state, clock.UtcNow + Ttl);
        return id;
    }

    public bool TryGet(string id, out PagerState state)
    {
        if (_states.TryGetValue(id, out var entry) && entry.Expires > clock.UtcNow)
        {
            state = entry.State;
            return true;
        }
        state = null!;
        return false;
    }

    private void Prune()
    {
        var now = clock.UtcNow;
        foreach (var (key, value) in _states)
            if (value.Expires <= now) _states.TryRemove(key, out _);
    }
}

