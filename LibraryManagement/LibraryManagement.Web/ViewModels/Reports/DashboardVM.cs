namespace LibraryManagement.Web.ViewModels.Reports;

/// <summary>
/// Dữ liệu tổng hợp hiển thị trên Dashboard của Thủ thư.
/// </summary>
public class DashboardVM
{
    /// <summary>Số đầu sách khác nhau.</summary>
    public long TotalBookTitles { get; set; }

    /// <summary>Tổng số cuốn sách (cộng tất cả TotalQuantity).</summary>
    public int TotalBookCopies { get; set; }

    /// <summary>Số độc giả có thẻ đang hoạt động (CardStatus = Active).</summary>
    public long ActiveReaders { get; set; }

    /// <summary>Số phiếu đang mượn (Status = Borrowing).</summary>
    public long BorrowingLoans { get; set; }

    /// <summary>Số phiếu quá hạn (Status = Borrowing và DueDate < hôm nay theo giờ VN).</summary>
    public long OverdueLoans { get; set; }

    /// <summary>Số yêu cầu gia hạn đang chờ duyệt (Pending).</summary>
    public long PendingRenewals { get; set; }

    /// <summary>Số yêu cầu đặt trước đang chờ duyệt (Pending).</summary>
    public long PendingReservations { get; set; }

    /// <summary>Số yêu cầu đặt trước đã duyệt (Approved) mà sách hiện có trong kho (AvailableQuantity > 0).</summary>
    public long ReadyReservations { get; set; }
}

