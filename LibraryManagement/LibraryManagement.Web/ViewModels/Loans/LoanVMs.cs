using System.ComponentModel.DataAnnotations;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Services;

namespace LibraryManagement.Web.ViewModels.Loans;

// ---------------------------------------------------------------- UC07 Lập phiếu mượn

/// <summary>UC07 bước 3: thông tin độc giả và kết quả kiểm tra điều kiện mượn.</summary>
public class BorrowerCheckVM
{
    public required Reader Reader { get; init; }

    /// <summary>Số cuốn đang mượn (chưa trả) trên mọi phiếu.</summary>
    public int BorrowingBooks { get; init; }

    public int OverdueLoans { get; init; }
    public int MaxBooks { get; init; }
    public int Remaining => Math.Max(0, MaxBooks - BorrowingBooks);

    /// <summary>Lý do không được mượn (bước 3a). Rỗng nghĩa là đủ điều kiện.</summary>
    public List<string> Reasons { get; init; } = new();

    public bool CanBorrow => Reasons.Count == 0;
}

/// <summary>Một đầu sách để chọn khi lập phiếu (bước 4).</summary>
public class BookOptionVM
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Authors { get; init; } = string.Empty;
    public int AvailableQuantity { get; init; }
    public int TotalQuantity { get; init; }
}

/// <summary>UC07: màn hình lập phiếu mượn (nhập mã thẻ → kiểm tra → chọn sách → hạn trả).</summary>
public class CreateLoanVM
{
    [Display(Name = "Mã thẻ độc giả")]
    public string? CardNumber { get; set; }

    [Display(Name = "Sách mượn")]
    public List<string> BookIds { get; set; } = new();

    [Display(Name = "Hạn trả")]
    [DataType(DataType.Date)]
    [Required(ErrorMessage = "Vui lòng chọn hạn trả.")]
    public DateTime? DueDate { get; set; }

    // Dữ liệu hiển thị, không nhận từ form
    public BorrowerCheckVM? Borrower { get; set; }
    public List<BookOptionVM> Books { get; set; } = new();
    public DateTime BorrowDate { get; set; }
    public int LoanDays { get; set; }
}

// ---------------------------------------------------------------- UC08 Nhận trả sách

/// <summary>Một cuốn chưa trả trong phiếu, kèm số liệu để tính phạt (bước 3, 5).</summary>
public class ReturnItemVM
{
    public string BookId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    /// <summary>Giá sách hiện tại, dùng khi mất sách (QĐ06).</summary>
    public long Price { get; init; }

    /// <summary>Phạt quá hạn của cuốn này nếu trả hôm nay (QĐ05).</summary>
    public long OverdueFine { get; init; }
}

/// <summary>Một phiếu đang mượn hiện ở màn hình nhận trả.</summary>
public class ReturnLoanVM
{
    public string LoanId { get; init; } = string.Empty;
    public string LoanCode { get; init; } = string.Empty;
    public string CardNumber { get; init; } = string.Empty;
    public string ReaderName { get; init; } = string.Empty;
    public DateTime BorrowDate { get; init; }
    public DateTime DueDate { get; init; }
    public int OverdueDays { get; init; }
    public int TotalItems { get; init; }
    public List<ReturnItemVM> Items { get; init; } = new();
}

/// <summary>UC08 bước 2–3: ô tìm theo mã thẻ hoặc mã phiếu và các phiếu đang mượn tìm được.</summary>
public class ReturnSearchVM
{
    [Display(Name = "Mã thẻ hoặc mã phiếu")]
    public string? Keyword { get; set; }

    public List<ReturnLoanVM> Loans { get; set; } = new();
    public long FinePerDay { get; set; }

    /// <summary>Thông báo khi không có gì để trả (bước 3a).</summary>
    public string? Notice { get; set; }
}

/// <summary>Một cuốn Thủ thư đánh dấu trả, gửi lên từ form (bước 4).</summary>
public class ReturnItemInput
{
    public string BookId { get; set; } = string.Empty;
    public bool Selected { get; set; }
    public BookCondition Condition { get; set; } = BookCondition.Normal;

    /// <summary>Mức phạt hư hỏng Thủ thư nhập (đồng). Chỉ dùng khi Condition = Damaged.</summary>
    public long? DamageFine { get; set; }
}

/// <summary>Form nhận trả của một phiếu.</summary>
public class ReturnLoanInput
{
    public string LoanId { get; set; } = string.Empty;
    public List<ReturnItemInput> Items { get; set; } = new();
}

/// <summary>Kết quả sau khi nhận trả (bước 7, 8, 7a). Lưu qua TempData dạng JSON để chuyển trang.</summary>
public class ReturnResultVM
{
    public string LoanId { get; set; } = string.Empty;
    public string LoanCode { get; set; } = string.Empty;
    public string ReaderName { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public List<ReturnedLine> Lines { get; set; } = new();
    public long TotalFine => Lines.Sum(l => l.Fine);

    /// <summary>Phiếu đã trả hết và chuyển sang Returned (bước 8).</summary>
    public bool LoanClosed { get; set; }
    public int RemainingItems { get; set; }

    /// <summary>Người đang chờ các sách vừa trả (bước 7a), sớm nhất lên đầu.</summary>
    public List<WaitingReader> Waiting { get; set; } = new();
}

public class ReturnedLine
{
    public string Title { get; set; } = string.Empty;
    public BookCondition Condition { get; set; }
    public long OverdueFine { get; set; }
    public long Fine { get; set; }
}

// ---------------------------------------------------------------- UC14 Sách của tôi

/// <summary>UC14 bước 4: một cuốn đã trả trong lịch sử mượn.</summary>
public class HistoryLineVM
{
    public string LoanCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime BorrowDate { get; init; }
    public DateTime ReturnDate { get; init; }
    public BookCondition? Condition { get; init; }
    public long Fine { get; init; }
}

// ---------------------------------------------------------------- UC15, UC09 Gia hạn

/// <summary>Một phiếu được xem xét gia hạn: dùng cho xác nhận của độc giả (UC15) và danh sách duyệt của Thủ thư (UC09).</summary>
public class RenewalVM
{
    public string LoanId { get; init; } = string.Empty;
    public string LoanCode { get; init; } = string.Empty;
    public string CardNumber { get; init; } = string.Empty;
    public string ReaderName { get; init; } = string.Empty;
    public List<string> Titles { get; init; } = new();
    public DateTime DueDate { get; init; }

    /// <summary>Hạn trả sau khi gia hạn (hạn hiện tại + RenewalDays).</summary>
    public DateTime NewDueDate { get; init; }

    public int RenewalCount { get; init; }
    public int MaxRenewals { get; init; }
    public int RenewalDays { get; init; }

    /// <summary>Thời điểm gửi yêu cầu đang chờ (nếu có).</summary>
    public DateTime? PendingRequestedAt { get; init; }

    /// <summary>Lý do không đủ điều kiện gia hạn (QĐ04). Rỗng nghĩa là được duyệt.</summary>
    public List<string> Problems { get; init; } = new();

    public bool Eligible => Problems.Count == 0;
}

/// <summary>UC09: danh sách yêu cầu chờ duyệt và khung gia hạn tại quầy (bước 1a).</summary>
public class RenewalIndexVM
{
    public List<RenewalVM> Pending { get; set; } = new();

    [Display(Name = "Mã phiếu")]
    public string? LoanCode { get; set; }

    public RenewalVM? Counter { get; set; }
    public string? CounterNotice { get; set; }
}
