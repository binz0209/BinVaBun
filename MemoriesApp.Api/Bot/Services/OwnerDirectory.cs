using BinVaBun.DiscordBot.Configuration;
using Microsoft.Extensions.Options;

namespace BinVaBun.DiscordBot.Services;

/// <summary>A person on the allowlist.</summary>
public sealed record Owner(ulong DiscordId, string Username, string Name)
{
    /// <summary>Stable lowercase token for store keys.</summary>
    public string KeyToken => DiscordId.ToString();
    public string Mention => $"<@{DiscordId}>";
}

/// <summary>
/// The explicit allowlist. Being in the Discord server grants nothing;
/// only IDs configured here may use the bot.
/// </summary>
public sealed class OwnerDirectory
{
    private readonly Dictionary<ulong, Owner> _owners;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public OwnerDirectory(IOptions<List<OwnerOptions>> options) : this(options.Value) { }

    public OwnerDirectory(IEnumerable<OwnerOptions> owners)
    {
        _owners = owners
            .Where(o => o.DiscordId != 0 && !string.IsNullOrWhiteSpace(o.Username))
            .GroupBy(o => o.DiscordId).Select(g => g.First())
            .ToDictionary(o => o.DiscordId, o => new Owner(o.DiscordId, o.Username.Trim(),
                string.IsNullOrWhiteSpace(o.Name) ? o.Username.Trim() : o.Name.Trim()));
    }

    public IReadOnlyCollection<Owner> All => _owners.Values;
    public bool IsOwner(ulong discordId) => _owners.ContainsKey(discordId);

    public Owner Get(ulong discordId) =>
        _owners.TryGetValue(discordId, out var owner)
            ? owner
            : throw new UnauthorizedAccessException("Not an allowlisted user.");

    public bool TryGet(ulong discordId, out Owner owner) => _owners.TryGetValue(discordId, out owner!);

    /// <summary>The other person, or null when the allowlist doesn't have exactly two people.</summary>
    public Owner? PartnerOf(ulong discordId) =>
        _owners.Count == 2 ? _owners.Values.FirstOrDefault(o => o.DiscordId != discordId) : null;

    /// <summary>Identity used for scheduled work that has no triggering user.</summary>
    public Owner System => _owners.Values.First();
}

