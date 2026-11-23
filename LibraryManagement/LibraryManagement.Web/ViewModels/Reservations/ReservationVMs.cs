using System.ComponentModel.DataAnnotations;
using LibraryManagement.Web.Models;

namespace LibraryManagement.Web.ViewModels.Reservations;

/// <summary>
/// Hiển thị 1 dòng trong danh sách "Đặt trước của tôi" phía Độc giả (UC16).
/// </summary>
public class MyReservationItemVM
{
    public string Id { get; set; } = string.Empty;
    public string BookId { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public DateTime ReservedAt { get; set; }
    public ReservationStatus Status { get; set; }
    public string? Reason { get; set; }

    /// <summary>Đặt giữ: ngày cuối được đến nhận sách.</summary>
    public DateTime? HoldUntil { get; set; }

    /// <summary>
    /// Độc giả được quyền tự hủy yêu cầu khi đang Chờ duyệt, Đã duyệt hoặc Đang giữ sách (UC16 bước 1b).
    /// </summary>
    public bool CanCancel => Status is ReservationStatus.Pending or ReservationStatus.Approved or ReservationStatus.Held;
}

/// <summary>
/// Màn hình xác nhận đặt trước sách phía Độc giả (UC16 bước 3).
/// </summary>
public class ReservationConfirmVM
{
    public string BookId { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public int? PublishYear { get; set; }
    public List<string> CategoryNames { get; set; } = new();
    public int AvailableQuantity { get; set; }

    /// <summary>
    /// true: sách còn và không ai đang xếp hàng → xác nhận là ĐẶT GIỮ (giữ 1 bản).
    /// false: sách hết hoặc đã có người chờ trước → xác nhận là ĐẶT TRƯỚC (vào hàng chờ).
    /// </summary>
    public bool WillHold { get; set; }

    /// <summary>Có độc giả khác đang xếp hàng chờ cuốn này.</summary>
    public bool HasQueue { get; set; }

    /// <summary>Ngày cuối được đến nhận nếu đặt giữ hôm nay (giờ VN).</summary>
    public DateTime HoldUntilPreview { get; set; }
    public int HoldDays { get; set; }
    public int MaxHolds { get; set; }
}

/// <summary>
/// Hiển thị thông tin yêu cầu đặt trước cho Thủ thư (UC10).
/// </summary>
public class LibrarianReservationVM
{
    public string Id { get; set; } = string.Empty;
    public string ReaderId { get; set; } = string.Empty;
    public string ReaderName { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string? ReaderPhone { get; set; }
    public string? ReaderEmail { get; set; }
    public CardStatus CardStatus { get; set; }
    public bool IsCardExpired { get; set; }

    public string BookId { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public DateTime ReservedAt { get; set; }
    public ReservationStatus Status { get; set; }
    public string? Reason { get; set; }
    public string? ProcessedBy { get; set; }
    public int AvailableQuantity { get; set; }

    /// <summary>Đặt giữ: ngày cuối được đến nhận sách.</summary>
    public DateTime? HoldUntil { get; set; }

    /// <summary>
    /// Đã duyệt và sách đã có trong kho (AvailableQuantity > 0) -> Cần liên hệ độc giả đến nhận (UC10 bước 3a).
    /// </summary>
    public bool IsReadyForPickup => Status == ReservationStatus.Approved && AvailableQuantity > 0;
}

/// <summary>
/// Danh sách quản lý đặt trước theo tab cho Thủ thư (UC10).
/// </summary>
public class LibrarianReservationListVM
{
    public List<LibrarianReservationVM> Items { get; set; } = new();
    public string CurrentTab { get; set; } = "all";

    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int ReadyCount { get; set; }
    public int HeldCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
}

/// <summary>
/// Form từ chối hoặc hủy yêu cầu đặt trước kèm lý do (UC10 bước 3b, 5a).
/// </summary>
public class ReservationActionVM
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lý do.")]
    [StringLength(200, ErrorMessage = "Lý do không quá 200 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

