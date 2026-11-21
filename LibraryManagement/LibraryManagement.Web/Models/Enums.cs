namespace LibraryManagement.Web.Models;

// Enum lưu dạng string trong MongoDB (đăng ký trong MongoConventions).

public enum Role { Librarian, Reader }

public enum CardStatus { Active, Locked }

public enum LoanStatus { Borrowing, Returned }

public enum BookCondition { Normal, Damaged, Lost }

public enum RenewalStatus { Pending, Approved, Rejected }

/// <summary>Nguồn của yêu cầu gia hạn: độc giả gửi online hay Thủ thư gia hạn tại quầy.</summary>
public enum RenewalSource { Online, Counter }

public enum ReservationStatus { Pending, Approved, Rejected, Completed, Cancelled }

/// <summary>Tên hiển thị tiếng Việt của các enum (mục 5.3 thiết kế kỹ thuật).</summary>
public static class EnumDisplay
{
    public static string ToVn(this Role value) => value switch
    {
        Role.Librarian => "Thủ thư",
        Role.Reader => "Độc giả",
        _ => value.ToString()
    };

    public static string ToVn(this CardStatus value) => value switch
    {
        CardStatus.Active => "Hoạt động",
        CardStatus.Locked => "Bị khóa",
        _ => value.ToString()
    };

    public static string ToVn(this LoanStatus value) => value switch
    {
        LoanStatus.Borrowing => "Đang mượn",
        LoanStatus.Returned => "Đã trả",
        _ => value.ToString()
    };

    public static string ToVn(this BookCondition value) => value switch
    {
        BookCondition.Normal => "Bình thường",
        BookCondition.Damaged => "Hư hỏng",
        BookCondition.Lost => "Mất",
        _ => value.ToString()
    };

    public static string ToVn(this RenewalStatus value) => value switch
    {
        RenewalStatus.Pending => "Chờ duyệt",
        RenewalStatus.Approved => "Đã duyệt",
        RenewalStatus.Rejected => "Từ chối",
        _ => value.ToString()
    };

    public static string ToVn(this RenewalSource value) => value switch
    {
        RenewalSource.Online => "Online",
        RenewalSource.Counter => "Tại quầy",
        _ => value.ToString()
    };

    public static string ToVn(this ReservationStatus value) => value switch
    {
        ReservationStatus.Pending => "Chờ duyệt",
        ReservationStatus.Approved => "Đã duyệt",
        ReservationStatus.Rejected => "Từ chối",
        ReservationStatus.Completed => "Hoàn tất",
        ReservationStatus.Cancelled => "Đã hủy",
        _ => value.ToString()
    };
}
