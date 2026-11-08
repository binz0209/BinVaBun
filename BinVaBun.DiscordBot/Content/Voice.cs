using System.Security.Cryptography;

namespace BinVaBun.DiscordBot.Content;

/// <summary>Toàn bộ câu thoại và văn phong của bot được quản lý ở đây để dễ dàng tinh chỉnh.</summary>
public static class Voice
{
    public static T Pick<T>(IReadOnlyList<T> items) => items[RandomNumberGenerator.GetInt32(items.Count)];

    public static readonly string[] Saved =
    [
        "Thêm một khoảnh khắc nhỏ được cất giữ an toàn. Sau này nhìn lại, tụi mình chắc chắn sẽ mỉm cười.",
        "Đã cất giữ rồi nè. Bot sẽ trông nom kỷ niệm này thật kỹ cho hai đứa nha.",
        "Khoảnh khắc đã được đưa vào hộp thời gian. Cảm ơn hai đứa vì đã không để nó trôi qua."
    ];

    public static readonly string[] Found =
    [
        "Xem bot tìm thấy gì trong hộp thời gian của hai đứa nè!",
        "Lúc đang dọn dẹp giá kỷ niệm thì chiếc này rơi ra nè.",
        "Kỷ niệm này vẫn luôn ở đây đợi hai đứa nhớ tới đó."
    ];

    public static readonly string[] OnThisDay =
    [
        "Vào ngày này năm ấy, có thể tụi mình đã thoáng quên, nhưng kỷ niệm nhỏ này vẫn nhớ rất rõ.",
        "Một trang ký ức của năm trước, tìm lại đúng vào ngày hôm nay nè.",
        "Cuốn lịch đã đánh dấu một trang thật đẹp, dành riêng cho hai đứa mình."
    ];

    public static readonly string[] NothingYet =
    [
        "Hộp thời gian của tụi mình vẫn còn trống. Hay là hôm nay ghi lại khoảnh khắc đầu tiên nha?",
        "Chưa có kỷ niệm nào trên kệ cả. Hai đứa mình bắt đầu bằng một điều nhỏ xinh nhé."
    ];

    public static readonly string[] NoMatches =
    [
        "Bot đã tìm khắp các ngăn kéo nhưng không thấy kỷ niệm nào theo từ khóa này cả.",
        "Không tìm thấy kỷ niệm nào phù hợp hết. Thử tìm bằng một từ khác xem sao nha?"
    ];

    public static readonly string[] NudgeNewMemory =
    [
        "Dạo này hai đứa chưa lưu thêm kỷ niệm nào nè. Hôm nay có khoảnh khắc nào đáng chụp một tấm hình không ta?",
        "Góc kỷ niệm dạo này im ắng quá nè. Gửi thêm chút gì mới cho bot giữ nhé!"
    ];

    public static readonly string[] AnniversarySoon =
    [
        "Một ngày đặc biệt đang đến rất gần rồi. Thêm một chút nữa là đến cột mốc tiếp theo của hai đứa mình.",
        "Bot vẫn đang âm thầm đếm ngược nè. Không còn lâu nữa đâu nha.",
        "Một trang mới trong câu chuyện tình yêu sắp mở ra rồi nè."
    ];

    public static readonly string[] AnniversaryToday =
    [
        "Hôm nay chính là ngày đó rồi! Thật hạnh phúc khi được cùng hai đứa lưu giữ câu chuyện này.",
        "Chính là hôm nay nè. Dù có bận rộn thế nào thì ngày này vẫn thật đặc biệt đối với hai đứa mình."
    ];

    public static readonly string[] DailyQuestionLead =
    [
        "Một câu hỏi nhỏ cho hai đứa ngày hôm nay nè.",
        "Cùng nhau suy ngẫm về điều này một chút nha.",
        "Câu hỏi nho nhỏ của ngày hôm nay."
    ];

    public static readonly string[] ReminderLead =
    [
        "Một lời nhắc nhẹ nhàng như đã hẹn nha.",
        "Hôm trước bạn có nhờ bot nhắc điều này nè.",
        "Còn nhớ chuyện này không ta?"
    ];

    public const string NotAllowed = "Góc nhỏ này chỉ dành riêng cho hai đứa mình thôi nè.";
    public const string Unexpected = "Có chút trục trặc nhỏ rồi. Hai đứa thử lại một lát nữa nha.";
    public const string Unreadable = "Bot chưa hiểu đoạn này lắm. Thử lại giúp bot nha!";
    public const string Expired = "Trang này đã hết hạn rồi. Hãy gõ lại lệnh để bot lấy lại trang mới nha.";
}
