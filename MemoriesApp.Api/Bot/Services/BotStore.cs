using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BinVaBun.DiscordBot.Models;

namespace BinVaBun.DiscordBot.Services;

/// <summary>
/// Typed access to the API's key/value store (/api/bot/store). All bot-owned data
/// (anniversaries, reminders, notes, answers, scheduler ledger) goes through here,
/// so the bot never needs database credentials.
/// </summary>
public sealed class BotStore(ApiTransport transport, OwnerDirectory owners)
{
    private sealed record Envelope<T>(string Kind, string Key, T Value);

    private static string Path(string kind, string key) =>
        $"api/bot/store/{Uri.EscapeDataString(kind)}/{Uri.EscapeDataString(key)}";

    private static StringContent JsonBody<T>(T value) =>
        new(JsonSerializer.Serialize(value, ApiTransport.Json), System.Text.Encoding.UTF8, "application/json");

    public async Task<T?> GetAsync<T>(string kind, string key, CancellationToken ct) where T : class
    {
        using var response = await transport.SendAsync(owners.System.Username, HttpMethod.Get, Path(kind, key), null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return (await ApiTransport.ReadAsync<Envelope<T>>(response, ct)).Value;
    }

    public async Task<bool> ExistsAsync(string kind, string key, CancellationToken ct) =>
        await GetAsync<object>(kind, key, ct) is not null;

    public async Task<IReadOnlyList<(string Key, T Value)>> ListAsync<T>(
        string kind, string? prefix = null, int limit = 200, bool descending = false, CancellationToken ct = default)
    {
        var path = $"api/bot/store/{Uri.EscapeDataString(kind)}?limit={limit}&descending={(descending ? "true" : "false")}";
        if (!string.IsNullOrEmpty(prefix)) path += "&prefix=" + Uri.EscapeDataString(prefix);

        using var response = await transport.SendAsync(owners.System.Username, HttpMethod.Get, path, null, ct);
        await ApiTransport.EnsureSuccessAsync(response, ct);
        var items = await ApiTransport.ReadAsync<List<Envelope<T>>>(response, ct);
        return items.Select(i => (i.Key, i.Value)).ToList();
    }

    public async Task UpsertAsync<T>(string kind, string key, T value, CancellationToken ct)
    {
        using var response = await transport.SendAsync(owners.System.Username, HttpMethod.Put, Path(kind, key), () => JsonBody(value), ct);
        await ApiTransport.EnsureSuccessAsync(response, ct);
    }

    /// <summary>Atomically create the record if absent. Returns false if it already existed.</summary>
    public async Task<bool> TryClaimAsync<T>(string kind, string key, T value, CancellationToken ct)
    {
        using var response = await transport.SendAsync(owners.System.Username, HttpMethod.Post, Path(kind, key) + "/claim", () => JsonBody(value), ct);
        if (response.StatusCode == HttpStatusCode.Conflict) return false;
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return true;
    }

    /// <summary>Returns true if something was deleted.</summary>
    public async Task<bool> DeleteAsync(string kind, string key, CancellationToken ct)
    {
        using var response = await transport.SendAsync(owners.System.Username, HttpMethod.Delete, Path(kind, key), null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return true;
    }
}
