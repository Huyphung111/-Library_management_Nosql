using System.Globalization;

namespace LibraryManagement.Web.Services;

/// <summary>Định dạng hiển thị dùng chung (mục 6.5 thiết kế kỹ thuật): tiền dạng 15.000đ, ngày dd/MM/yyyy theo giờ VN.</summary>
public static class DisplayFormat
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>15000 → "15.000đ".</summary>
    public static string ToMoney(this long amount) => amount.ToString("#,##0", Vi) + "đ";

    /// <summary>Giá trị UTC lưu trong database → "dd/MM/yyyy" theo giờ VN.</summary>
    public static string ToVnDateText(this DateTime utc) => DateHelper.ToVnDate(utc).ToString("dd/MM/yyyy");

    /// <summary>Thời điểm UTC → "dd/MM/yyyy HH:mm" theo giờ VN.</summary>
    public static string ToVnDateTimeText(this DateTime utc) => DateHelper.ToVnTime(utc).ToString("dd/MM/yyyy HH:mm");
}
