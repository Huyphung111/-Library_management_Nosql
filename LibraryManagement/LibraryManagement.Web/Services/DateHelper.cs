namespace LibraryManagement.Web.Services;

/// <summary>
/// Hàm tính ngày dùng chung, ở MỘT chỗ duy nhất (mục 6.4 thiết kế kỹ thuật).
/// Quy ước: mọi thời điểm lưu UTC; field kiểu "ngày" lưu 00:00 giờ Việt Nam quy ra UTC.
/// </summary>
public static class DateHelper
{
    private static readonly TimeZoneInfo VnZone = FindVnZone();

    private static TimeZoneInfo FindVnZone()
    {
        // Windows dùng "SE Asia Standard Time", Linux/macOS dùng "Asia/Ho_Chi_Minh".
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam");
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    /// <summary>Giờ hiện tại theo giờ Việt Nam.</summary>
    public static DateTime NowVn() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnZone);

    /// <summary>Ngày hôm nay theo giờ Việt Nam (00:00, Kind = Unspecified).</summary>
    public static DateTime TodayVn() => NowVn().Date;

    /// <summary>Đổi một thời điểm UTC (lấy từ database) sang NGÀY theo giờ Việt Nam.</summary>
    public static DateTime ToVnDate(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), VnZone).Date;

    /// <summary>Đổi một ngày theo giờ Việt Nam sang giá trị UTC để lưu (00:00 giờ VN của ngày đó).</summary>
    public static DateTime VnDateToUtc(DateTime vnDate) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(vnDate.Date, DateTimeKind.Unspecified), VnZone);

    /// <summary>Thời điểm UTC bắt đầu ngày hôm nay (giờ VN). Dùng làm mốc trong truy vấn "quá hạn", "hết hạn".</summary>
    public static DateTime StartOfTodayVnUtc() => VnDateToUtc(TodayVn());

    /// <summary>Số ngày quá hạn của một hạn trả tính đến hôm nay theo giờ VN. 0 nếu chưa quá hạn.</summary>
    public static int DaysOverdue(DateTime dueDateUtc)
    {
        var days = (TodayVn() - ToVnDate(dueDateUtc)).Days;
        return days > 0 ? days : 0;
    }
}
