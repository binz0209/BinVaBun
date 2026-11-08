using BinVaBun.DiscordBot.Configuration;
using BinVaBun.DiscordBot.Hosting;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BinVaBun.DiscordBot.Modules;

/// <summary>
/// Shared plumbing for every module. Concrete modules MUST carry [RequireCouple]
/// (a unit test enforces it). Dependencies arrive through property injection.
/// </summary>
public abstract class CompanionModuleBase : InteractionModuleBase<SocketInteractionContext>
{
    public MemoriesApiClient Api { get; set; } = null!;
    public OwnerDirectory Owners { get; set; } = null!;
    public IClock Clock { get; set; } = null!;

    private CancellationTokenSource? _cts;

    /// <summary>Every command gets a generous but finite deadline; deferred interactions live for 15 minutes.</summary>
    protected CancellationToken Ct => (_cts ??= new CancellationTokenSource(TimeSpan.FromMinutes(10))).Token;

    /// <summary>The allowlisted person who triggered this interaction.</summary>
    protected Owner Me => Owners.Get(Context.User.Id);

    public override Task AfterExecuteAsync(ICommandInfo command)
    {
        _cts?.Dispose();
        return base.AfterExecuteAsync(command);
    }
}

/// <summary>Autocomplete handlers don't run module preconditions, so they re-check the allowlist themselves.</summary>
public abstract class GuardedAutocompleteHandler : AutocompleteHandler
{
    protected static bool IsAllowed(IInteractionContext context, IServiceProvider services, out Owner owner)
    {
        owner = null!;
        var guild = services.GetRequiredService<IOptions<DiscordOptions>>().Value.GuildId;
        return context.Guild?.Id == guild
               && services.GetRequiredService<OwnerDirectory>().TryGet(context.User.Id, out owner);
    }

    protected static string Label(string text, int max = 100) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}

