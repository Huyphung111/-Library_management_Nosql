using System.ComponentModel.DataAnnotations;
using LibraryManagement.Web.Models;

namespace LibraryManagement.Web.ViewModels.Readers;

// UC06: một dòng trong danh sách độc giả.
public class ReaderListVM
{
    public string Id { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }

    /// <summary>Ngày hết hạn theo giờ VN (để hiển thị dd/MM/yyyy).</summary>
    public DateTime ExpiryDate { get; set; }

    public CardStatus CardStatus { get; set; }

    /// <summary>Thẻ hết hạn, tính khi truy vấn.</summary>
    public bool IsExpired { get; set; }
}

// UC06: trang danh sách, kèm từ khóa và bộ lọc trạng thái.
public class ReaderIndexVM
{
    public string? Keyword { get; set; }

    /// <summary>"" (tất cả), "active", "locked", "expired".</summary>
    public string? Status { get; set; }

    public List<ReaderListVM> Items { get; set; } = new();
}

// UC06: form thêm / sửa độc giả (bước 4, 3a). Kiểm tra trùng CCCD nằm trong ReaderService.
public class ReaderFormVM
{
    public string? Id { get; set; }

    /// <summary>Chỉ để hiển thị khi sửa.</summary>
    public string? CardNumber { get; set; }

    [Display(Name = "Họ tên")]
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, ErrorMessage = "Họ tên không quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Ngày sinh")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Số CCCD")]
    [Required(ErrorMessage = "Vui lòng nhập số CCCD.")]
    [RegularExpression(@"^\d{12}$", ErrorMessage = "Số CCCD gồm đúng 12 chữ số.")]
    public string IdNumber { get; set; } = string.Empty;

    [Display(Name = "Số điện thoại")]
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại gồm 10 chữ số, bắt đầu bằng 0.")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(100, ErrorMessage = "Email không quá 100 ký tự.")]
    public string? Email { get; set; }

    [Display(Name = "Địa chỉ")]
    [StringLength(200, ErrorMessage = "Địa chỉ không quá 200 ký tự.")]
    public string? Address { get; set; }
}

// UC06: trang chi tiết độc giả (các thao tác 3b–3e đặt ở đây). Cũng là trang đích khi Tìm kiếm (UC11) chọn một độc giả.
public class ReaderDetailsVM
{
    public required Reader Reader { get; init; }
    public string? Username { get; init; }
    public bool IsExpired => Reader.IsExpired;

    /// <summary>Các phiếu mượn của độc giả, mới nhất trước.</summary>
    public List<Loan> Loans { get; init; } = new();

    /// <summary>Số cuốn đang mượn (chưa trả).</summary>
    public int BorrowingBooks { get; init; }

    /// <summary>Có thông tin tài khoản vừa cấp (bước 8) để hiện một lần sau khi thêm.</summary>
    public bool JustIssued { get; set; }
    public string? DefaultPassword { get; set; }
}
