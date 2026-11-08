using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Xunit;

namespace BinVaBun.DiscordBot.Tests;

public class AnniversaryCalculatorTests
{
    private static Anniversary A(string date, bool yearly = true) => new() { Date = DateOnly.Parse(date), Yearly = yearly };

    [Fact]
    public void Yearly_date_later_this_year()
        => Assert.Equal(new DateOnly(2026, 12, 24), AnniversaryCalculator.NextOccurrence(A("2022-12-24"), new DateOnly(2026, 11, 8)));

    [Fact]
    public void Yearly_date_already_passed_rolls_to_next_year()
        => Assert.Equal(new DateOnly(2027, 2, 14), AnniversaryCalculator.NextOccurrence(A("2024-02-14"), new DateOnly(2026, 11, 8)));

    [Fact]
    public void Today_counts_as_next()
        => Assert.Equal(new DateOnly(2026, 11, 8), AnniversaryCalculator.NextOccurrence(A("2020-11-08"), new DateOnly(2026, 11, 8)));

    [Fact]
    public void Leap_day_falls_back_to_feb_28_in_common_years()
        => Assert.Equal(new DateOnly(2027, 2, 28), AnniversaryCalculator.NextOccurrence(A("2024-02-29"), new DateOnly(2026, 11, 8)));

    [Fact]
    public void One_off_in_future_is_itself_and_in_past_is_null()
    {
        var today = new DateOnly(2026, 11, 8);
        Assert.Equal(new DateOnly(2027, 1, 1), AnniversaryCalculator.NextOccurrence(A("2027-01-01", yearly: false), today));
        Assert.Null(AnniversaryCalculator.NextOccurrence(A("2026-01-01", yearly: false), today));
    }

    [Fact]
    public void Yearly_date_in_the_future_is_not_moved_earlier()
        => Assert.Equal(new DateOnly(2030, 5, 5), AnniversaryCalculator.NextOccurrence(A("2030-05-05"), new DateOnly(2026, 11, 8)));

    [Fact]
    public void Years_and_breakdown()
    {
        Assert.Equal(4, AnniversaryCalculator.YearsAt(A("2022-12-24"), new DateOnly(2026, 12, 24)));
        Assert.Equal((1, 2, 3), AnniversaryCalculator.Breakdown(new DateOnly(2026, 1, 1), new DateOnly(2027, 3, 4)));
    }
}

public class FlexibleParserTests
{
    private static readonly TimeZoneInfo Zone = TimeZones.Resolve("Asia/Ho_Chi_Minh");
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 15, 0, 0, TimeSpan.FromHours(7));

    [Theory]
    [InlineData("2026-12-24", 2026, 12, 24)]
    [InlineData("24/12/2026", 2026, 12, 24)]
    [InlineData("4/1/2025", 2025, 1, 4)]
    public void Parses_dates(string input, int y, int m, int d)
    {
        Assert.True(FlexibleParser.TryParseDate(input, out var date));
        Assert.Equal(new DateOnly(y, m, d), date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("2026-13-40")]
    public void Rejects_bad_dates(string input) => Assert.False(FlexibleParser.TryParseDate(input, out _));

    [Fact]
    public void Absolute_datetime_is_interpreted_in_the_configured_zone()
    {
        Assert.True(FlexibleParser.TryParseDateTime("2026-12-24 19:30", Now, Zone, out var r));
        Assert.Equal(new DateTimeOffset(2026, 12, 24, 12, 30, 0, TimeSpan.Zero), r.ToUniversalTime());
    }

    [Theory]
    [InlineData("in 2h", 120)]
    [InlineData("in 30m", 30)]
    [InlineData("in 2 days", 2 * 24 * 60)]
    public void Relative_times(string input, int minutes)
    {
        Assert.True(FlexibleParser.TryParseDateTime(input, Now, Zone, out var r));
        Assert.Equal(Now.AddMinutes(minutes), r);
    }

    [Fact]
    public void Bare_time_means_next_occurrence()
    {
        Assert.True(FlexibleParser.TryParseDateTime("09:00", Now, Zone, out var r)); // already past today
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.FromHours(7)), r);
        Assert.True(FlexibleParser.TryParseDateTime("tomorrow 07:15", Now, Zone, out r));
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 7, 15, 0, TimeSpan.FromHours(7)), r);
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("25:00")]
    [InlineData("")]
    public void Rejects_bad_datetimes(string input) => Assert.False(FlexibleParser.TryParseDateTime(input, Now, Zone, out _));

    [Theory]
    [InlineData("20:00", true)]
    [InlineData("8:05", true)]
    [InlineData("24:00", false)]
    [InlineData("noon", false)]
    public void Time_of_day(string input, bool ok) => Assert.Equal(ok, FlexibleParser.TryParseTimeOfDay(input, out _));
}

public class ScheduleMathTests
{
    private static DateTimeOffset At(int h, int m) => new(2026, 11, 8, h, m, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Not_due_before_the_time() => Assert.False(ScheduleMath.IsDue(At(19, 59), new TimeOnly(20, 0), TimeSpan.FromHours(6)));

    [Fact]
    public void Due_at_and_after_the_time_within_the_window()
    {
        Assert.True(ScheduleMath.IsDue(At(20, 0), new TimeOnly(20, 0), TimeSpan.FromHours(6)));
        Assert.True(ScheduleMath.IsDue(At(23, 30), new TimeOnly(20, 0), TimeSpan.FromHours(6))); // bot restarted late
    }

    [Fact]
    public void Not_due_when_far_past_the_window() => Assert.False(ScheduleMath.IsDue(At(23, 30), new TimeOnly(9, 0), TimeSpan.FromHours(6)));
}

public class OwnerDirectoryTests
{
    private static OwnerDirectory Two() => new(new[]
    {
        new BinVaBun.DiscordBot.Configuration.OwnerOptions { DiscordId = 1, Username = "Hlinh" },
        new BinVaBun.DiscordBot.Configuration.OwnerOptions { DiscordId = 2, Username = "Pthao", Name = "Thao" }
    });

    [Fact]
    public void Only_allowlisted_ids_pass()
    {
        var owners = Two();
        Assert.True(owners.IsOwner(1));
        Assert.False(owners.IsOwner(3));
        Assert.False(owners.IsOwner(0));
        Assert.Throws<UnauthorizedAccessException>(() => owners.Get(3));
    }

    [Fact]
    public void Partner_is_the_other_person_and_names_default_to_username()
    {
        var owners = Two();
        Assert.Equal(2UL, owners.PartnerOf(1)!.DiscordId);
        Assert.Equal("Hlinh", owners.Get(1).Name);
        Assert.Equal("Thao", owners.Get(2).Name);
    }

    [Fact]
    public void No_partner_unless_exactly_two_owners()
    {
        var one = new OwnerDirectory(new[] { new BinVaBun.DiscordBot.Configuration.OwnerOptions { DiscordId = 1, Username = "a" } });
        Assert.Null(one.PartnerOf(1));
    }

    [Fact]
    public void Incomplete_entries_are_ignored()
    {
        var owners = new OwnerDirectory(new[]
        {
            new BinVaBun.DiscordBot.Configuration.OwnerOptions { DiscordId = 0, Username = "ghost" },
            new BinVaBun.DiscordBot.Configuration.OwnerOptions { DiscordId = 5, Username = " " }
        });
        Assert.Empty(owners.All);
    }
}

public class ImageValidationTests
{
    private const string Url = "https://cdn.discordapp.com/attachments/1/2/photo.png";

    [Fact]
    public void Accepts_a_normal_png() => ImageAttachmentService.ValidateMetadata("photo.png", "image/png", 1024, Url);

    [Theory]
    [InlineData("photo.exe", "image/png", 1024, Url)]               // wrong extension
    [InlineData("photo.png", "application/pdf", 1024, Url)]          // wrong content type
    [InlineData("photo.png", "image/png", 0, Url)]                   // empty
    [InlineData("photo.png", "image/png", 11 * 1024 * 1024, Url)]    // too big
    [InlineData("photo.png", "image/png", 1024, "http://cdn.discordapp.com/a.png")]   // not https
    [InlineData("photo.png", "image/png", 1024, "https://evil.example.com/a.png")]    // SSRF attempt
    [InlineData("photo.png", "image/png", 1024, "https://169.254.169.254/latest")]    // metadata service
    public void Rejects_bad_attachments(string name, string type, long size, string url)
        => Assert.Throws<UserInputException>(() => ImageAttachmentService.ValidateMetadata(name, type, size, url));

    [Fact]
    public void Sniffs_real_formats_and_rejects_impostors()
    {
        Assert.Equal(".png", ImageAttachmentService.Sniff([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])!.Value.Extension);
        Assert.Equal(".jpg", ImageAttachmentService.Sniff([0xFF, 0xD8, 0xFF, 0xE0])!.Value.Extension);
        Assert.Equal(".gif", ImageAttachmentService.Sniff("GIF89a"u8)!.Value.Extension);
        Assert.Equal(".webp", ImageAttachmentService.Sniff("RIFF\0\0\0\0WEBPVP8 "u8)!.Value.Extension);
        Assert.Null(ImageAttachmentService.Sniff("MZ\x90\0this is an exe"u8));
        Assert.Null(ImageAttachmentService.Sniff([]));
    }
}

public class ContentTests
{
    [Fact]
    public void Daily_question_is_stable_per_date_and_varies_across_days()
    {
        var d = new DateOnly(2026, 11, 8);
        Assert.Equal(BinVaBun.DiscordBot.Content.QuestionBank.ForDate(d), BinVaBun.DiscordBot.Content.QuestionBank.ForDate(d));
        var window = Enumerable.Range(0, BinVaBun.DiscordBot.Content.QuestionBank.Count)
            .Select(i => BinVaBun.DiscordBot.Content.QuestionBank.ForDate(d.AddDays(i))).ToList();
        Assert.Equal(window.Count, window.Distinct().Count()); // no repeats within one full cycle
    }

    [Theory]
    [InlineData(0, 0, 3, true)]
    [InlineData(0, 5, 2, false)]
    [InlineData(0, 20, 1, false)]
    [InlineData(0, 200, 0, false)]
    public void Quiz_scoring(int _, int daysOff, int points, bool exact)
    {
        var actual = new DateOnly(2025, 8, 14);
        var (p, e) = BinVaBun.DiscordBot.Modules.QuizScoring.ScoreDate(actual.AddDays(-daysOff), actual);
        Assert.Equal((points, exact), (p, e));
    }

    [Fact]
    public void DotEnv_does_not_override_real_environment()
    {
        Environment.SetEnvironmentVariable("BVB_TEST_A", "real");
        Environment.SetEnvironmentVariable("BVB_TEST_B", null);
        BinVaBun.DiscordBot.Extensions.DotEnv.Apply("BVB_TEST_A=fromfile");
        BinVaBun.DiscordBot.Extensions.DotEnv.Apply("BVB_TEST_B=\"quoted value\"");
        BinVaBun.DiscordBot.Extensions.DotEnv.Apply("# comment");
        Assert.Equal("real", Environment.GetEnvironmentVariable("BVB_TEST_A"));
        Assert.Equal("quoted value", Environment.GetEnvironmentVariable("BVB_TEST_B"));
    }

    [Fact]
    public void Memory_id_validation_blocks_path_tricks()
    {
        Assert.True(MemoriesApiClient.IsValidId("64b7f0c2a1b2c3d4e5f60718"));
        Assert.False(MemoriesApiClient.IsValidId("../store/settings"));
        Assert.False(MemoriesApiClient.IsValidId("abc"));
        Assert.False(MemoriesApiClient.IsValidId(null));
    }
}

