using Discord.Interactions;

namespace BinVaBun.DiscordBot.Content;

/// <summary>Các câu hỏi gợi mở về tình yêu, kỷ niệm, ước mơ và tương lai của hai đứa.</summary>
public static class QuestionBank
{
    private static readonly string[] All =
    [
        "Khoảnh khắc nhỏ nào trong tháng này mà bạn muốn lưu giữ mãi mãi?",
        "Ấn tượng đầu tiên của bạn về đối phương là gì?",
        "Kỷ niệm nào của hai đứa khiến bạn bất giác mỉm cười nhanh nhất?",
        "Bạn mong muốn một năm sau kể từ hôm nay, hai đứa mình sẽ ở đâu?",
        "Bài hát nào mỗi khi nghe đều khiến bạn nhớ ngay đến tụi mình?",
        "Có điều gì mình hay làm mà bạn thầm thích mê không?",
        "Nếu có một cuối tuần rảnh rỗi không có kế hoạch gì trước, bạn muốn hai đứa làm gì?",
        "Có điều gì bạn chưa từng kể với mình mà bây giờ muốn chia sẻ không?",
        "Bữa ăn cùng nhau nào khiến bạn nhớ nhất?",
        "Nơi nào bạn muốn hai đứa mình cùng đặt chân đến trước khi già đi?",
        "Lần gần nhất bạn cảm thấy thật gắn kết và gần gũi với mình là khi nào?",
        "Có điều gì mới mẻ mà bạn muốn hai đứa cùng thử trong mùa này không?",
        "Chuyện hài hước hoặc ngố tàu nhất từng xảy ra giữa hai đứa là gì?",
        "Bạn nghĩ ngôi nhà tương lai của tụi mình sẽ có mùi hương như thế nào?",
        "Thói quen nhỏ nào của hai đứa mà bạn mong sẽ không bao giờ thay đổi?",
        "Nếu được tua lại một ngày bất kỳ của tụi mình, bạn sẽ chọn ngày nào?",
        "Hiện tại điều gì khiến bạn háo hức và mong chờ nhất?",
        "Món quà tuyệt vời nhất mà bạn từng nhận được từ mình là gì (dù lớn hay nhỏ)?",
        "Điều gì ở mình khiến bạn cảm thấy tự hào nhất?",
        "Nếu câu chuyện của hai đứa là một bộ phim, nó sẽ có tựa đề là gì?",
        "Ước mơ nào bạn ấp ủ bấy lâu nhưng chưa từng nói thành lời?",
        "Có một điều gì mà hai đứa mình nên làm thường xuyên hơn?",
        "Bạn đã nghĩ gì trong lần đầu tiên hai đứa nói chuyện thâu đêm suốt sáng?",
        "Bức ảnh nào của hai đứa là bức bạn thích nhất, và vì sao?",
        "Một ngày lười biếng hoàn hảo cùng nhau của hai đứa sẽ như thế nào?",
        "Có điều gì về bản thân mà bạn nhận ra hoặc học được kể từ khi bên nhau?",
        "Khó khăn lớn nhất mà hai đứa mình đã cùng nhau vượt qua là gì?",
        "Nếu tiền bạc không thành vấn đề, bạn muốn đưa mình đi đâu?",
        "Có truyền thống nhỏ nào mà bạn muốn hai đứa bắt đầu duy trì không?",
        "Trò đùa ngầm nào của hai đứa đến giờ vẫn làm bạn bật cười?",
        "Một điều bạn muốn hai đứa khắc ghi mãi về năm nay là gì?",
        "Điều dịu dàng hoặc ân cần nhất mà mình từng làm cho bạn là gì?",
        "Những lúc xa nhau, điều gì ở đối phương làm bạn nhớ nhất?",
        "Bạn mong kỷ niệm ngày yêu tiếp theo của hai đứa sẽ có cảm giác như thế nào?",
        "Mùi hương, âm thanh hay hương vị nào đưa bạn trở về ngay một khoảnh khắc bên mình?",
        "Kỹ năng nào bạn muốn hai đứa mình cùng nhau học?",
        "Có điều gì bạn muốn gửi một lời cảm ơn chân thành đến mình không?",
        "Chuyến đi tiếp theo mà bạn muốn hai đứa mình cùng lên kế hoạch là ở đâu?",
        "Bạn sẽ miêu tả tình yêu của hai đứa như thế nào với một người chưa từng gặp tụi mình?",
        "Một điều bé xíu nào đã làm ngày hôm nay của bạn trở nên tươi đẹp hơn?"
    ];

    private static readonly int[] Order = Shuffle(All.Length);

    private static int[] Shuffle(int n)
    {
        var order = Enumerable.Range(0, n).ToArray();
        var rng = new Random(20240214); // fixed seed: same sequence on every restart
        for (var i = n - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order;
    }

    /// <summary>Deterministic per date, so no state is needed and no question repeats before the bank is exhausted.</summary>
    public static string ForDate(DateOnly date) => All[Order[((date.DayNumber % All.Length) + All.Length) % All.Length]];

    public static int Count => All.Length;
}

public enum DateMood
{
    [ChoiceDisplay("Ngẫu nhiên")] Any,
    [ChoiceDisplay("Ấm cúng")] Cozy,
    [ChoiceDisplay("Ngoài trời")] Outdoors,
    [ChoiceDisplay("Sáng tạo")] Creative,
    [ChoiceDisplay("Ẩm thực")] Food
}

public static class DateIdeas
{
    private static readonly (DateMood Mood, string Idea)[] All =
    [
        (DateMood.Cozy, "Cùng nhau dựng lều chăn nệm ấm cúng và chọn xem một bộ phim mà cả hai chưa từng xem."),
        (DateMood.Cozy, "Pha đồ uống nóng, tắt hết đèn và lần lượt bật những bài hát mình yêu thích cho nhau nghe."),
        (DateMood.Cozy, "Đọc to lại những dòng tin nhắn thuở mới quen của hai đứa."),
        (DateMood.Cozy, "Một buổi tối không chạm vào điện thoại, cùng nhau chơi board game hoặc bài tây."),
        (DateMood.Cozy, "Viết một lá thư ngắn gửi bản thân trong tương lai và niêm phong lại để mở ra vào năm sau."),
        (DateMood.Cozy, "Cùng nhau xem lại những bức ảnh cũ và chọn ra 5 tấm đẹp nhất lưu vào app."),
        (DateMood.Outdoors, "Dạo bước đến một nơi hai đứa chưa từng tới, và lúc về không dùng bản đồ xem sao."),
        (DateMood.Outdoors, "Ngắm hoàng hôn cùng nhau tại một góc mới toanh mà hai đứa chưa từng thử."),
        (DateMood.Outdoors, "Mang theo bình giữ nhiệt ra công viên ngồi ngắm dòng người qua lại."),
        (DateMood.Outdoors, "Đạp xe không cần đích đến, thấy chỗ nào xinh xắn thì tạt vào ghé chơi."),
        (DateMood.Outdoors, "Ghé thăm lại nơi hẹn hò đầu tiên của hai đứa và chụp lại cùng một kiểu ảnh ngày ấy."),
        (DateMood.Outdoors, "Cùng nhau ngắm bầu trời đêm và nói to một điều ước của mình."),
        (DateMood.Creative, "Vẽ chân dung đối phương từ trí nhớ trong 5 phút, rồi cùng so sánh tác phẩm."),
        (DateMood.Creative, "Tạo một danh sách phát những bài hát của tụi mình và viết một dòng cảm xúc cho mỗi bài."),
        (DateMood.Creative, "Lần lượt viết tiếp một câu chuyện: mỗi người tiếp một câu xem kết cục ra sao."),
        (DateMood.Creative, "Thử làm một món đồ thủ công đơn giản theo hướng dẫn trên mạng và giữ làm kỷ niệm."),
        (DateMood.Creative, "Lên kế hoạch cho một chuyến du lịch tưởng tượng: chọn đất nước, ngân sách và 3 điểm dừng chân đầu tiên."),
        (DateMood.Creative, "Chụp 10 bức ảnh đối phương làm những việc bình dị thường ngày, rồi chọn tấm dễ thương nhất lưu lại."),
        (DateMood.Food, "Cùng nhau nấu một món ăn đặc sản của một quốc gia mà hai đứa chưa từng đặt chân đến."),
        (DateMood.Food, "Ghé một quán ăn ven đường mà hai đứa hay đi ngang qua nhưng chưa vào, và gọi món theo gợi ý của chủ quán."),
        (DateMood.Food, "Cùng nhau làm một món bánh ngọt, dù thành quả có vụng về cũng không sao."),
        (DateMood.Food, "Một bữa tối phá cách: ăn món tráng miệng trước rồi mới ăn món chính."),
        (DateMood.Food, "Mỗi người bí mật chọn một nguyên liệu bất ngờ, rồi cùng nấu bữa tối kết hợp cả hai."),
        (DateMood.Food, "Thử một quán cà phê mới toanh và đóng vai những nhà phê bình ẩm thực để chấm điểm đồ uống.")
    ];

    public static string Random(DateMood mood)
    {
        var pool = mood == DateMood.Any ? All : All.Where(x => x.Mood == mood).ToArray();
        return pool[System.Security.Cryptography.RandomNumberGenerator.GetInt32(pool.Length)].Idea;
    }
}
