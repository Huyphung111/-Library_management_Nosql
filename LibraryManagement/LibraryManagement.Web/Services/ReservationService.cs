using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;

namespace LibraryManagement.Web.Services;

/// <summary>
/// Một người đang chờ sách (yêu cầu đặt trước Approved), có sẵn thông tin liên hệ lấy từ readers.
/// Dùng ở UC08 bước 7a (A hiển thị) và UC10 bước 3a (B hiển thị). Chốt ngày 21/11/2026, phan-cong.md mục 3.
/// </summary>
public class WaitingReader
{
    public string ReservationId { get; init; } = string.Empty;
    public string BookId { get; init; } = string.Empty;
    public string BookTitle { get; init; } = string.Empty;
    public string ReaderId { get; init; } = string.Empty;
    public string CardNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }

    /// <summary>Thời điểm đặt (UTC). Danh sách sắp theo trường này tăng dần: người đặt sớm nhất lên đầu.</summary>
    public DateTime ReservedAt { get; init; }
}

// UC10, UC16: đặt trước. (B viết ở đợt 2)
public class ReservationService
{
    private readonly MongoDbContext _db;
    private readonly LoanService _loans;

    public ReservationService(MongoDbContext db, LoanService loans)
    {
        _db = db;
        _loans = loans;
    }

    // ------------------------------------------------------------------
    // Hàm giao kèo cho A (phan-cong.md mục 3). TẠM TRẢ GIÁ TRỊ GIẢ,
    // B viết thật ở đợt 2. Không đổi chữ ký.
    // ------------------------------------------------------------------

    /// <summary>
    /// UC07: độc giả vừa mượn các sách này thì chuyển yêu cầu đặt trước Pending/Approved tương ứng sang Completed,
    /// ghi completedLoanId. Chạy trong session (transaction) của A. Trả về số yêu cầu đã chuyển.
    /// </summary>
    public Task<long> CompleteForLoanAsync(
        IClientSessionHandle session, string readerId, IEnumerable<string> bookIds, string loanId)
        => Task.FromResult(0L);

    /// <summary>
    /// UC08: người đang chờ các sách vừa trả (yêu cầu Approved), sắp theo reservedAt tăng dần,
    /// kèm họ tên, SĐT, email lấy từ readers.
    /// </summary>
    public Task<List<WaitingReader>> GetWaitingListAsync(IEnumerable<string> bookIds)
        => Task.FromResult(new List<WaitingReader>());

    /// <summary>UC09, UC15: các sách này có người đang chờ (đặt trước còn hiệu lực) không.</summary>
    public Task<bool> HasWaitingAsync(IEnumerable<string> bookIds) => Task.FromResult(false);
}
