using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BinVaBun.DiscordBot.Models;
using Microsoft.Extensions.Logging;

namespace BinVaBun.DiscordBot.Services;

/// <summary>
/// Low-level HTTP access to MemoriesApp.Api. Adds the bot credentials, retries idempotent
/// calls on transient failures (the API host can cold-start) and translates failures into
/// <see cref="CompanionException"/>s with user-safe messages. Never logs bodies.
/// </summary>
public sealed class ApiTransport(HttpClient http, ILogger<ApiTransport> logger)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <summary>Waits between attempts for idempotent calls (cold starts usually need a few seconds).</summary>
    public TimeSpan[] RetryDelays { get; init; } = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6)];

    public async Task<HttpResponseMessage> SendAsync(
        string actingUser, HttpMethod method, string path, Func<HttpContent?>? content, CancellationToken ct)
    {
        // Only methods that are safe to repeat get retried; creating a memory must never be duplicated.
        var idempotent = method == HttpMethod.Get || method == HttpMethod.Put || method == HttpMethod.Delete;
        var attempts = idempotent ? RetryDelays.Length + 1 : 1;

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, path) { Content = content?.Invoke() };
            request.Headers.Add("X-Bot-User", actingUser);
            try
            {
                var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
                var transient = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                                || (int)response.StatusCode >= 500;
                if (!transient) return response;

                if (attempt >= attempts)
                {
                    response.Dispose();
                    throw new ApiUnavailableException();
                }
                logger.LogWarning("API {Method} {Path} returned {Status}; retrying (attempt {Attempt}/{Max})",
                    method, path, (int)response.StatusCode, attempt, attempts);
                response.Dispose();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= attempts)
                {
                    logger.LogError(ex, "API {Method} {Path} unreachable after {Attempts} attempt(s)", method, path, attempt);
                    throw new ApiUnavailableException(ex);
                }
                logger.LogWarning("API {Method} {Path} failed ({Error}); retrying (attempt {Attempt}/{Max})",
                    method, path, ex.GetType().Name, attempt, attempts);
            }
            await Task.Delay(RetryDelays[attempt - 1], ct);
        }
    }

    /// <summary>Throws a friendly exception unless the response is 2xx. 404 must be handled by callers first.</summary>
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                throw new ApiAuthException();
            case HttpStatusCode.BadRequest or HttpStatusCode.NotFound:
                throw new ApiRejectedException(await ReadErrorAsync(response, ct) ?? "That didn't go through. Please check what you entered.");
            default:
                throw new ApiUnavailableException();
        }
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
            {
                var text = e.GetString();
                return string.IsNullOrWhiteSpace(text) || text.Length > 300 ? null : text;
            }
        }
        catch (JsonException) { }
        return null;
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                   ?? throw new ApiUnavailableException();
        }
        catch (JsonException ex)
        {
            throw new ApiUnavailableException(ex);
        }
    }
}
