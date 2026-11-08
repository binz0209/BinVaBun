namespace BinVaBun.DiscordBot.Models;

/// <summary>
/// Lỗi an toàn được hiển thị trực tiếp cho người dùng trong Discord.
/// Những ngoại lệ không thuộc <see cref="CompanionException"/> sẽ được ghi log và thay bằng thông báo chung.
/// </summary>
public class CompanionException : Exception
{
    public string UserMessage { get; }

    public CompanionException(string userMessage, Exception? inner = null) : base(userMessage, inner)
        => UserMessage = userMessage;
}

/// <summary>Dữ liệu người dùng nhập không hợp lệ (sai định dạng ngày, tệp không đúng,...).</summary>
public sealed class UserInputException(string message) : CompanionException(message);

/// <summary>Không kết nối được dịch vụ Memories API.</summary>
public sealed class ApiUnavailableException(Exception? inner = null)
    : CompanionException("Hiện tại bot chưa kết nối được với hộp kỷ niệm. Hai đứa đợi một lát rồi thử lại nha.", inner);

/// <summary>Sai thông tin xác thực API key của bot.</summary>
public sealed class ApiAuthException()
    : CompanionException("Hộp kỷ niệm không nhận diện được bot. Hãy kiểm tra lại API key giúp bot nhé.");

/// <summary>Dịch vụ API trả về lỗi nghiệp vụ có thông điệp cụ thể.</summary>
public sealed class ApiRejectedException(string message) : CompanionException(message);
