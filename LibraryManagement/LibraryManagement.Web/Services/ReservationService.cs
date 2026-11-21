using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;

namespace LibraryManagement.Web.Services;

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

    /// <summary>UC08: các yêu cầu Approved của những sách vừa trả, sắp theo reservedAt tăng dần.</summary>
    public Task<List<Reservation>> GetWaitingListAsync(IEnumerable<string> bookIds)
        => Task.FromResult(new List<Reservation>());

    /// <summary>UC09, UC15: các sách này có người đang chờ (đặt trước còn hiệu lực) không.</summary>
    public Task<bool> HasWaitingAsync(IEnumerable<string> bookIds) => Task.FromResult(false);
}
