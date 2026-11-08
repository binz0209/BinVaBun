using BinVaBun.DiscordBot.Configuration;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BinVaBun.DiscordBot.Services;

/// <summary>Completes once the gateway is connected and slash commands are registered.</summary>
public sealed class BotReadiness
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Ready => _ready.Task;
    public void SetReady() => _ready.TrySetResult();
}

/// <summary>Sends scheduled/unsolicited messages (channel posts and DMs).</summary>
public sealed class DiscordNotifier(DiscordSocketClient client, ILogger<DiscordNotifier> logger)
{
    public async Task SendToChannelAsync(
        ulong channelId, string? text = null, Embed[]? embeds = null,
        MessageComponent? components = null, IEnumerable<ulong>? mentionUsers = null)
    {
        var channel = client.GetChannel(channelId) as IMessageChannel
                      ?? await client.GetChannelAsync(channelId) as IMessageChannel
                      ?? throw new InvalidOperationException($"Channel {channelId} is not reachable by the bot.");

        var userList = mentionUsers?.ToList();
        var mentions = userList != null && userList.Count > 0
            ? new AllowedMentions { UserIds = userList }
            : AllowedMentions.None;
        await channel.SendMessageAsync(text, embeds: embeds, components: components, allowedMentions: mentions);
    }

    /// <summary>Returns false if the user has DMs closed or the DM could not be sent.</summary>
    public async Task<bool> TrySendDmAsync(ulong userId, Embed embed)
    {
        try
        {
            var user = await client.GetUserAsync(userId);
            if (user == null) return false;
            var dm = await user.CreateDMChannelAsync();
            await dm.SendMessageAsync(embed: embed);
            return true;
        }
        catch (HttpException ex)
        {
            logger.LogWarning("Could not DM user {UserId} (Discord error {Code})", userId, (int)ex.DiscordCode!);
            return false;
        }
    }
}

