using BinVaBun.DiscordBot.Configuration;
using BinVaBun.DiscordBot.Content;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BinVaBun.DiscordBot.Hosting;

/// <summary>
/// Gate for every command, component, modal and autocomplete: the caller must be on the
/// allowlist AND be using the configured private server. Server membership alone grants nothing.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireCoupleAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        var owners = services.GetRequiredService<OwnerDirectory>();
        var guild = services.GetRequiredService<IOptions<DiscordOptions>>().Value.GuildId;
        var allowed = owners.IsOwner(context.User.Id) && context.Guild?.Id == guild;
        return Task.FromResult(allowed ? PreconditionResult.FromSuccess() : PreconditionResult.FromError(Voice.NotAllowed));
    }
}

/// <summary>Owns the Discord connection, slash-command registration and central error handling.</summary>
public sealed class DiscordBotService(
    DiscordSocketClient client,
    InteractionService interactions,
    IServiceProvider services,
    IOptions<DiscordOptions> discordOptions,
    OwnerDirectory owners,
    MemoriesApiClient api,
    BotReadiness readiness,
    ILogger<DiscordBotService> logger) : IHostedService
{
    private int _registered;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (owners.All.Count == 0)
            throw new InvalidOperationException("No owners configured. Set Owners__0__DiscordId/Username (and Owners__1__...).");
        if (owners.All.Count != 2)
            logger.LogWarning("{Count} owner(s) configured; features that involve a partner (love notes) need exactly two", owners.All.Count);

        client.Log += OnDiscordLog;
        client.Ready += () => { _ = Task.Run(OnReadyAsync, CancellationToken.None); return Task.CompletedTask; };
        client.InteractionCreated += OnInteractionAsync;
        interactions.InteractionExecuted += OnExecutedAsync;
        interactions.Log += OnDiscordLog;

        await interactions.AddModulesAsync(typeof(DiscordBotService).Assembly, services);

        await client.LoginAsync(TokenType.Bot, discordOptions.Value.Token);
        await client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
        await client.LogoutAsync();
    }

    private async Task OnReadyAsync()
    {
        try
        {
            if (Interlocked.Exchange(ref _registered, 1) == 0)
            {
                await interactions.RegisterCommandsToGuildAsync(discordOptions.Value.GuildId, deleteMissing: true);
                logger.LogInformation("Slash commands registered to guild {GuildId}", discordOptions.Value.GuildId);
            }

            try
            {
                var me = await api.GetMeAsync(owners.System.Username, CancellationToken.None);
                logger.LogInformation("Memory service reachable (time zone {Zone})", me.TimeZone);
            }
            catch (CompanionException ex)
            {
                logger.LogWarning("Memory service check failed at startup: {Message}. Commands will retry on demand.", ex.UserMessage);
            }

            readiness.SetReady();
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _registered, 0);
            logger.LogCritical(ex, "Failed to register slash commands");
        }
    }

    private async Task OnInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(client, interaction);
            await interactions.ExecuteCommandAsync(context, services);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error dispatching interaction {Type}", interaction.Type);
        }
    }

    /// <summary>Single place where every failure becomes a friendly message; internals are only logged.</summary>
    private async Task OnExecutedAsync(ICommandInfo command, IInteractionContext context, IResult result)
    {
        if (result.IsSuccess) return;

        string message;
        switch (result.Error)
        {
            case InteractionCommandError.UnmetPrecondition:
                logger.LogWarning("Blocked {Command} for non-allowlisted user {UserId}", command?.Name, context.User.Id);
                message = Voice.NotAllowed;
                break;
            case InteractionCommandError.Exception when result is ExecuteResult { Exception: { } wrapped }:
                var ex = wrapped is InteractionException { InnerException: { } inner } ? inner : wrapped;
                if (ex is CompanionException ce)
                {
                    logger.LogInformation("{Command} ended with a user-facing error: {Type}", command?.Name, ce.GetType().Name);
                    message = ce.UserMessage;
                }
                else
                {
                    logger.LogError(ex, "{Command} failed unexpectedly", command?.Name);
                    message = Voice.Unexpected;
                }
                break;
            default:
                logger.LogWarning("{Command} could not be executed: {Error}", command?.Name, result.Error);
                message = Voice.Unreadable;
                break;
        }

        await RespondWithErrorAsync(context.Interaction, message);
    }

    private async Task RespondWithErrorAsync(IDiscordInteraction interaction, string message)
    {
        try
        {
            var embed = Embeds.Plain(message);
            if (interaction.Type == InteractionType.ApplicationCommandAutocomplete) return;

            if (!interaction.HasResponded)
                await interaction.RespondAsync(embed: embed, ephemeral: true);
            else if (interaction.Type == InteractionType.MessageComponent)
                await interaction.FollowupAsync(embed: embed, ephemeral: true);
            else
                await interaction.ModifyOriginalResponseAsync(p => { p.Content = null; p.Embed = embed; p.Embeds = null; p.Components = new ComponentBuilder().Build(); });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not deliver an error message (the interaction may have expired)");
        }
    }

    private Task OnDiscordLog(LogMessage msg)
    {
        var level = msg.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            _ => LogLevel.Debug
        };
        logger.Log(level, msg.Exception, "[{Source}] {Message}", msg.Source, msg.Message);
        return Task.CompletedTask;
    }
}

