using Microsoft.Extensions.Options;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Options;

namespace LibraryManagement.Web.Services;

// UC07, UC08, UC09, UC14, UC15: mượn trả, gia hạn. (A viết ở đợt 2 và 3)
public class LoanService
{
    private readonly MongoDbContext _db;
    private readonly LibraryRules _rules;
    private readonly CounterService _counters;

    // Lazy để tránh vòng phụ thuộc: ReservationService cũng inject LoanService (UC16 gọi IsBorrowingAsync).
    // Dùng _reservations.Value khi cần gọi CompleteForLoanAsync, GetWaitingListAsync, HasWaitingAsync.
    private readonly Lazy<ReservationService> _reservations;

    public LoanService(
        MongoDbContext db,
        IOptions<LibraryRules> rules,
        CounterService counters,
        Lazy<ReservationService> reservations)
    {
        _db = db;
        _rules = rules.Value;
        _counters = counters;
        _reservations = reservations;
    }

    // ------------------------------------------------------------------
    // Hàm giao kèo cho B (phan-cong.md mục 3). TẠM TRẢ GIÁ TRỊ GIẢ,
    // A viết thật ở đợt 2. Không đổi chữ ký.
    // ------------------------------------------------------------------

    /// <summary>Độc giả có đang mượn cuốn sách này (phiếu Borrowing, item chưa trả) không. B gọi trong UC16.</summary>
    public Task<bool> IsBorrowingAsync(string readerId, string bookId) => Task.FromResult(false);

    /// <summary>Sách có nằm trong phiếu mượn đang mở (Borrowing) nào không. B gọi khi xóa sách ở UC04.</summary>
    public Task<bool> HasOpenLoanForBookAsync(string bookId) => Task.FromResult(false);
}
