using System.Text.Json;
using System.Text.RegularExpressions;
using MemoriesApp.Api.DTOs;
using MemoriesApp.Api.Models;
using MemoriesApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace MemoriesApp.Api.Controllers;

/// <summary>
/// Small key/value store (collection <c>botRecords</c>) for data owned by the Discord bot.
/// Keeps the bot free of database credentials. Records are addressed by (kind, key).
/// </summary>
[ApiController]
[Route("api/bot/store")]
[Authorize(AuthenticationSchemes = BotApiKeyAuthenticationHandler.SchemeName)]
public partial class BotStoreController : ControllerBase
{
    private const int MaxPayloadChars = 16 * 1024;

    [GeneratedRegex("^[a-z0-9-]{1,40}$")]
    private static partial Regex KindPattern();

    [GeneratedRegex("^[A-Za-z0-9:_.\\-]{1,120}$")]
    private static partial Regex KeyPattern();

    private readonly MongoDbService _mongoDb;

    public BotStoreController(MongoDbService mongoDb) => _mongoDb = mongoDb;

    private static BotRecordDto ToDto(BotRecord r)
    {
        using var doc = JsonDocument.Parse(r.Json);
        return new BotRecordDto
        {
            Kind = r.Kind,
            Key = r.Key,
            Value = doc.RootElement.Clone(),
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };
    }

    private IActionResult? Validate(string kind, string? key = null)
    {
        if (!KindPattern().IsMatch(kind)) return BadRequest(new { error = "Invalid kind." });
        if (key != null && !KeyPattern().IsMatch(key)) return BadRequest(new { error = "Invalid key." });
        return null;
    }

    [HttpGet("{kind}")]
    public async Task<IActionResult> List(string kind, [FromQuery] string? prefix = null,
        [FromQuery] int limit = 100, [FromQuery] bool descending = false, CancellationToken ct = default)
    {
        if (Validate(kind) is { } invalid) return invalid;
        limit = Math.Clamp(limit, 1, 500);

        var f = Builders<BotRecord>.Filter;
        var filter = f.Eq(r => r.Kind, kind);
        if (!string.IsNullOrEmpty(prefix))
            filter &= f.Regex(r => r.Key, new MongoDB.Bson.BsonRegularExpression("^" + Regex.Escape(prefix)));

        var find = _mongoDb.BotRecords.Find(filter);
        find = descending ? find.SortByDescending(r => r.Key) : find.SortBy(r => r.Key);
        var records = await find.Limit(limit).ToListAsync(ct);
        return Ok(records.Select(ToDto));
    }

    [HttpGet("{kind}/{key}")]
    public async Task<IActionResult> Get(string kind, string key, CancellationToken ct)
    {
        if (Validate(kind, key) is { } invalid) return invalid;
        var record = await _mongoDb.BotRecords.Find(r => r.Kind == kind && r.Key == key).FirstOrDefaultAsync(ct);
        return record == null ? NotFound() : Ok(ToDto(record));
    }

    [HttpPut("{kind}/{key}")]
    public async Task<IActionResult> Upsert(string kind, string key, [FromBody] JsonElement value, CancellationToken ct)
    {
        if (Validate(kind, key) is { } invalid) return invalid;
        var raw = value.GetRawText();
        if (raw.Length > MaxPayloadChars) return BadRequest(new { error = "Payload too large." });

        var now = DateTime.UtcNow;
        var update = Builders<BotRecord>.Update
            .Set(r => r.Json, raw)
            .Set(r => r.UpdatedAt, now)
            .SetOnInsert(r => r.Kind, kind)
            .SetOnInsert(r => r.Key, key)
            .SetOnInsert(r => r.CreatedAt, now);
        await _mongoDb.BotRecords.UpdateOneAsync(r => r.Kind == kind && r.Key == key, update,
            new UpdateOptions { IsUpsert = true }, ct);
        return NoContent();
    }

    /// <summary>Insert only if absent. 201 = claimed, 409 = someone already holds it.</summary>
    [HttpPost("{kind}/{key}/claim")]
    public async Task<IActionResult> Claim(string kind, string key, [FromBody] JsonElement value, CancellationToken ct)
    {
        if (Validate(kind, key) is { } invalid) return invalid;
        var raw = value.GetRawText();
        if (raw.Length > MaxPayloadChars) return BadRequest(new { error = "Payload too large." });

        try
        {
            await _mongoDb.BotRecords.InsertOneAsync(new BotRecord { Kind = kind, Key = key, Json = raw }, cancellationToken: ct);
            return StatusCode(StatusCodes.Status201Created);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return Conflict();
        }
    }

    [HttpDelete("{kind}/{key}")]
    public async Task<IActionResult> Delete(string kind, string key, CancellationToken ct)
    {
        if (Validate(kind, key) is { } invalid) return invalid;
        var result = await _mongoDb.BotRecords.DeleteOneAsync(r => r.Kind == kind && r.Key == key, ct);
        return result.DeletedCount > 0 ? NoContent() : NotFound();
    }
}

