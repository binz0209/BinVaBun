using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BinVaBun.DiscordBot.Tests;

/// <summary>Stands in for MemoriesApp.Api: an in-memory /api/bot/store plus programmable failures.</summary>
public sealed class FakeApiHandler : HttpMessageHandler
{
    private readonly object _gate = new();
    public Dictionary<string, string> Records { get; } = new();
    public List<(HttpMethod Method, string Path)> Requests { get; } = new();
    public Func<HttpRequestMessage, HttpResponseMessage?>? Override { get; set; }
    public int FailNext { get; set; }
    public HttpStatusCode FailStatus { get; set; } = HttpStatusCode.ServiceUnavailable;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);

        lock (_gate)
        {
            Requests.Add((request.Method, path));
            if (FailNext > 0)
            {
                FailNext--;
                return new HttpResponseMessage(FailStatus);
            }
            if (Override?.Invoke(request) is { } custom) return custom;

            var m = Regex.Match(path, @"^api/bot/store/([^/]+)/([^/]+?)(/claim)?$");
            if (!m.Success) return new HttpResponseMessage(HttpStatusCode.NotFound);
            var id = $"{Uri.UnescapeDataString(m.Groups[1].Value)}|{Uri.UnescapeDataString(m.Groups[2].Value)}";

            if (m.Groups[3].Success && request.Method == HttpMethod.Post)
            {
                if (Records.ContainsKey(id)) return new HttpResponseMessage(HttpStatusCode.Conflict);
                Records[id] = body!;
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
            if (request.Method == HttpMethod.Put) { Records[id] = body!; return new HttpResponseMessage(HttpStatusCode.NoContent); }
            if (request.Method == HttpMethod.Delete)
                return new HttpResponseMessage(Records.Remove(id) ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
            if (request.Method == HttpMethod.Get)
            {
                if (!Records.TryGetValue(id, out var json)) return new HttpResponseMessage(HttpStatusCode.NotFound);
                var envelope = $"{{\"kind\":\"x\",\"key\":\"y\",\"value\":{json}}}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope, Encoding.UTF8, "application/json") };
            }
            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }
    }

    public static HttpResponseMessage Json(HttpStatusCode status, object payload) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
}

