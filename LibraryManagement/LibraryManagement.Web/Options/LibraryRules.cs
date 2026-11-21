namespace LibraryManagement.Web.Options;

/// <summary>
/// Quy định nghiệp vụ QĐ01–QĐ10, đọc từ mục "LibraryRules" trong appsettings.json.
/// Không viết cứng các con số này trong code, hãy inject IOptions&lt;LibraryRules&gt;.
/// </summary>
public class LibraryRules
{
    /// <summary>Số năm hiệu lực của thẻ thư viện.</summary>
    public int CardValidYears { get; set; } = 1;

    /// <summary>Số sách tối đa một độc giả được mượn cùng lúc.</summary>
    public int MaxBooksPerReader { get; set; } = 5;

    /// <summary>Số ngày mượn mặc định (dùng để đề xuất hạn trả).</summary>
    public int LoanDays { get; set; } = 14;

    /// <summary>Số lần gia hạn tối đa của một phiếu mượn.</summary>
    public int MaxRenewals { get; set; } = 2;

    /// <summary>Số ngày được cộng thêm vào hạn trả cho mỗi lần gia hạn.</summary>
    public int RenewalDays { get; set; } = 7;

    /// <summary>Tiền phạt cho mỗi ngày trả trễ (đồng).</summary>
    public long FinePerDay { get; set; } = 5000;

    /// <summary>Độ dài tối thiểu của mật khẩu.</summary>
    public int MinPasswordLength { get; set; } = 6;

    /// <summary>Mật khẩu mặc định khi cấp tài khoản hoặc đặt lại mật khẩu cho độc giả.</summary>
    public string DefaultPassword { get; set; } = "123456";
}
