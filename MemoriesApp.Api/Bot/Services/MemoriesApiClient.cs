using System.Globalization;
using System.Net;
using BinVaBun.DiscordBot.Models;

namespace BinVaBun.DiscordBot.Services;

/// <summary>Typed client for the memory endpoints under /api/bot on MemoriesApp.Api.</summary>
public sealed class MemoriesApiClient(ApiTransport transport)
{
    public async Task<ApiIdentity> GetMeAsync(string username, CancellationToken ct)
    {
        using var response = await transport.SendAsync(username, HttpMethod.Get, "api/bot/me", null, ct);
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return await ApiTransport.ReadAsync<ApiIdentity>(response, ct);
    }

    public async Task<MemoryPage> ListAsync(string username, MemoryQuery q, CancellationToken ct)
    {
        var parts = new List<string>
        {
            $"page={q.Page}", $"pageSize={q.PageSize}", $"order={(q.Ascending ? "asc" : "desc")}"
        };
        if (!string.IsNullOrWhiteSpace(q.Query)) parts.Add("query=" + Uri.EscapeDataString(q.Query));
        if (!string.IsNullOrWhiteSpace(q.Tag)) parts.Add("tag=" + Uri.EscapeDataString(q.Tag));
        if (q.Year is { } y) parts.Add($"year={y}");
        if (q.Month is { } m) parts.Add($"month={m}");
        if (q.HasImage) parts.Add("hasImage=true");

        using var response = await transport.SendAsync(username, HttpMethod.Get, "api/bot/memories?" + string.Join('&', parts), null, ct);
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return await ApiTransport.ReadAsync<MemoryPage>(response, ct);
    }

    public async Task<Memory?> GetRandomAsync(string username, bool hasImage, IEnumerable<string>? exclude, CancellationToken ct)
    {
        var path = "api/bot/memories/random?hasImage=" + (hasImage ? "true" : "false");
        var excluded = exclude?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        if (excluded is { Length: > 0 }) path += "&exclude=" + Uri.EscapeDataString(string.Join(',', excluded));

        using var response = await transport.SendAsync(username, HttpMethod.Get, path, null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return await ApiTransport.ReadAsync<Memory>(response, ct);
    }

    public async Task<IReadOnlyList<Memory>> OnThisDayAsync(string username, DateOnly? date, CancellationToken ct)
    {
        var path = "api/bot/memories/on-this-day";
        if (date is { } d) path += $"?month={d.Month}&day={d.Day}";
        using var response = await transport.SendAsync(username, HttpMethod.Get, path, null, ct);
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return await ApiTransport.ReadAsync<List<Memory>>(response, ct);
    }

    public async Task<Memory?> GetAsync(string username, string id, CancellationToken ct)
    {
        if (!IsValidId(id)) return null;
        using var response = await transport.SendAsync(username, HttpMethod.Get, "api/bot/memories/" + id, null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return await ApiTransport.ReadAsync<Memory>(response, ct);
    }

    public async Task<Memory> CreateAsync(string username, CreateMemoryRequest request, CancellationToken ct)
    {
        HttpContent Build()
        {
            var form = new MultipartFormDataContent { { new StringContent(request.Title), "Title" } };
            if (!string.IsNullOrWhiteSpace(request.Description)) form.Add(new StringContent(request.Description), "Content");
            if (request.Date is { } d) form.Add(new StringContent(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "Date");
            if (!string.IsNullOrWhiteSpace(request.Tags)) form.Add(new StringContent(request.Tags), "Tags");
            if (request.Image is { } img)
            {
                var file = new ByteArrayContent(img.Bytes);
                file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(img.ContentType);
                form.Add(file, "Image", img.FileName);
            }
            return form;
        }

        using var response = await transport.SendAsync(username, HttpMethod.Post, "api/bot/memories", Build, ct);
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return await ApiTransport.ReadAsync<Memory>(response, ct);
    }

    public async Task<DatingStats> GetDatingStatsAsync(string username, int? year = null, int? month = null, CancellationToken ct = default)
    {
        var path = "api/bot/dating";
        var query = new List<string>();
        if (year.HasValue) query.Add($"year={year.Value}");
        if (month.HasValue) query.Add($"month={month.Value}");
        if (query.Count > 0) path += "?" + string.Join("&", query);

        using var response = await transport.SendAsync(username, HttpMethod.Get, path, null, ct);
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return await ApiTransport.ReadAsync<DatingStats>(response, ct);
    }

    public async Task<MarkDateResult> MarkDatingDateAsync(string username, string? date = null, CancellationToken ct = default)
    {
        HttpContent Build() => new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { date }),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await transport.SendAsync(username, HttpMethod.Post, "api/bot/dating/mark", Build, ct);
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return await ApiTransport.ReadAsync<MarkDateResult>(response, ct);
    }

    public async Task<MarkDateResult> UnmarkDatingDateAsync(string username, string date, CancellationToken ct = default)
    {
        using var response = await transport.SendAsync(username, HttpMethod.Delete, $"api/bot/dating/{date}", null, ct);
        await ApiTransport.EnsureSuccessAsync(response, ct);
        return await ApiTransport.ReadAsync<MarkDateResult>(response, ct);
    }

    /// <summary>Memory ids are Mongo ObjectIds; reject anything else before it reaches a URL.</summary>
    public static bool IsValidId(string? id) =>
        id is { Length: 24 } && id.All(Uri.IsHexDigit);
}

