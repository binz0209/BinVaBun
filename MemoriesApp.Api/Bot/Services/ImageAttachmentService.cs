using BinVaBun.DiscordBot.Models;

namespace BinVaBun.DiscordBot.Services;

/// <summary>Validates and downloads Discord attachments before anything is forwarded to the API.</summary>
public sealed class ImageAttachmentService(IHttpClientFactory httpFactory)
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public const string HttpClientName = "discord-cdn";

    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "cdn.discordapp.com", "media.discordapp.net"
    };

    /// <summary>Cheap pre-check using the metadata Discord sends; throws <see cref="UserInputException"/>.</summary>
    public static void ValidateMetadata(string? fileName, string? contentType, long size, string url)
    {
        if (size <= 0) throw new UserInputException("Tệp ảnh có vẻ trống rỗng rồi.");
        if (size > MaxBytes) throw new UserInputException("Tấm ảnh này lớn hơn 10 MB rồi. Gửi cho bot ảnh nhẹ hơn một chút nha!");
        if (contentType != null && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new UserInputException("Tệp này hình như không phải là ảnh nè.");
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".gif" or ".webp"))
            throw new UserInputException("Bot chỉ có thể lưu ảnh định dạng JPG, PNG, GIF hoặc WebP thôi nha.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !AllowedHosts.Contains(uri.Host))
            throw new UserInputException("Bot chỉ có thể lấy ảnh được tải trực tiếp lên Discord thôi.");
    }

    /// <summary>Identifies the real image type from magic bytes (never trusts the client-provided name/type).</summary>
    public static (string Extension, string ContentType)? Sniff(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return (".jpg", "image/jpeg");
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return (".png", "image/png");
        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8') return (".gif", "image/gif");
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            return (".webp", "image/webp");
        return null;
    }

    public async Task<ImageUpload> DownloadAsync(string url, string? fileName, string? contentType, long size, CancellationToken ct)
    {
        ValidateMetadata(fileName, contentType, size, url);

        var http = httpFactory.CreateClient(HttpClientName);
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new UserInputException("Bot không tải được ảnh từ Discord về rồi.");
        if (response.Content.Headers.ContentLength > MaxBytes) throw new UserInputException("Tấm ảnh này vượt quá dung lượng 10 MB rồi.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes) throw new UserInputException("Tấm ảnh này vượt quá dung lượng 10 MB rồi.");
        }

        var bytes = buffer.ToArray();
        var kind = Sniff(bytes) ?? throw new UserInputException("Tệp này không phải là ảnh hợp lệ.");
        return new ImageUpload(bytes, "discord-upload" + kind.Extension, kind.ContentType);
    }
}

