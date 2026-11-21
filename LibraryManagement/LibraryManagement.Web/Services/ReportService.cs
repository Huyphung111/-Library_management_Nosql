using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.ViewModels.Reports;

namespace LibraryManagement.Web.Services;

/// <summary>
/// UC12: Báo cáo, thống kê và số liệu tổng quan Dashboard (Member B).
/// </summary>
public class ReportService
{
    private readonly MongoDbContext _db;

    public ReportService(MongoDbContext db) => _db = db;

    /// <summary>
    /// Lấy toàn bộ số liệu thống kê tổng quan cho màn hình Dashboard của Thủ thư.
    /// </summary>
    public async Task<DashboardVM> GetDashboardAsync()
    {
        var vm = new DashboardVM();

        // 1. Số đầu sách và tổng số cuốn sách
        vm.TotalBookTitles = await _db.Books.CountDocumentsAsync(_ => true);

        var copyCountGroup = await _db.Books.Aggregate()
            .Group(b => 1, g => new { TotalCopies = g.Sum(x => x.TotalQuantity) })
            .FirstOrDefaultAsync();
        vm.TotalBookCopies = copyCountGroup?.TotalCopies ?? 0;

        // 2. Số độc giả đang hoạt động
        vm.ActiveReaders = await _db.Readers.CountDocumentsAsync(r => r.CardStatus == CardStatus.Active);

        // 3. Số phiếu đang mượn
        vm.BorrowingLoans = await _db.Loans.CountDocumentsAsync(l => l.Status == LoanStatus.Borrowing);

        // 4. Số phiếu quá hạn: status = Borrowing và DueDate < StartOfTodayVnUtc
        var todayUtc = DateHelper.StartOfTodayVnUtc();
        vm.OverdueLoans = await _db.Loans.CountDocumentsAsync(l =>
            l.Status == LoanStatus.Borrowing && l.DueDate < todayUtc);

        // 5. Yêu cầu gia hạn chờ duyệt: phiếu đang mượn có phần tử RenewalRequests mang status Pending
        vm.PendingRenewals = await _db.Loans.CountDocumentsAsync(l =>
            l.Status == LoanStatus.Borrowing &&
            l.RenewalRequests.Any(r => r.Status == RenewalStatus.Pending));

        // 6. Yêu cầu đặt trước chờ duyệt
        vm.PendingReservations = await _db.Reservations.CountDocumentsAsync(r =>
            r.Status == ReservationStatus.Pending);

        // 7. Yêu cầu đặt trước đã duyệt nhưng hiện sách đã có trong kho cần liên hệ độc giả
        var approvedReservations = await _db.Reservations
            .Find(r => r.Status == ReservationStatus.Approved)
            .Project(r => r.BookId)
            .ToListAsync();

        if (approvedReservations.Count > 0)
        {
            var distinctBookIds = approvedReservations.Distinct().ToList();
            var inStockBookIds = await _db.Books
                .Find(b => distinctBookIds.Contains(b.Id!) && b.AvailableQuantity > 0)
                .Project(b => b.Id)
                .ToListAsync();

            var inStockSet = inStockBookIds.ToHashSet();
            vm.ReadyReservations = approvedReservations.Count(id => inStockSet.Contains(id));
        }

        return vm;
    }
}
