using MongoDB.Bson;
using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.ViewModels.Reports;

namespace LibraryManagement.Web.Services;

/// <summary>
/// UC12: Báo cáo, thống kê và số liệu tổng quan Dashboard (Member B).
/// Sử dụng các MongoDB Aggregation Pipeline chuyên sâu theo thiết kế kỹ thuật mục 7.4.
/// </summary>
public class ReportService
{
    private readonly MongoDbContext _db;

    public ReportService(MongoDbContext db) => _db = db;

    #region Dashboard

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

    #endregion

    #region UC12 - Báo cáo Thống kê & Aggregations

    /// <summary>
    /// Báo cáo 1: Top 10 cuốn sách mượn nhiều nhất trong khoảng thời gian.
    /// Pipeline: $match (borrowDate) -> $unwind: "$Items" -> $group theo Items.BookId -> $sort count desc -> $limit 10
    /// </summary>
    public async Task<TopBooksReportVM> GetTopBooksAsync(DateTime? fromDate, DateTime? toDate)
    {
        var vm = new TopBooksReportVM
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            vm.ErrorMessage = "Ngày bắt đầu không được lớn hơn ngày kết thúc.";
            return vm;
        }

        var pipeline = new List<BsonDocument>();

        // 1. $match khoảng ngày mượn
        var matchFilter = new BsonDocument();
        if (fromDate.HasValue || toDate.HasValue)
        {
            var dateFilter = new BsonDocument();
            if (fromDate.HasValue)
                dateFilter.Add("$gte", BsonDateTime.Create(DateHelper.VnDateToUtc(fromDate.Value)));
            if (toDate.HasValue)
                dateFilter.Add("$lte", BsonDateTime.Create(DateHelper.VnDateToUtc(toDate.Value.AddDays(1).AddSeconds(-1))));
            matchFilter.Add("BorrowDate", dateFilter);
        }

        if (matchFilter.ElementCount > 0)
        {
            pipeline.Add(new BsonDocument("$match", matchFilter));
        }

        // 2. $unwind items
        pipeline.Add(new BsonDocument("$unwind", "$Items"));

        // 3. $group theo Items.BookId
        pipeline.Add(new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$Items.BookId" },
            { "count", new BsonDocument("$sum", 1) },
            { "title", new BsonDocument("$first", "$Items.Title") }
        }));

        // 4. $sort giảm dần và $limit 10
        pipeline.Add(new BsonDocument("$sort", new BsonDocument("count", -1)));
        pipeline.Add(new BsonDocument("$limit", 10));

        var aggResults = await _db.Loans.Aggregate<BsonDocument>(pipeline).ToListAsync();
        if (aggResults.Count == 0) return vm;

        var bookIds = aggResults.Select(d => d["_id"].AsString).ToList();
        var books = await _db.Books.Find(b => bookIds.Contains(b.Id!)).ToListAsync();
        var bookMap = books.ToDictionary(b => b.Id!);

        var categories = await _db.Categories.Find(_ => true).ToListAsync();
        var categoryMap = categories.ToDictionary(c => c.Id!, c => c.Name);

        foreach (var doc in aggResults)
        {
            var bookId = doc["_id"].AsString;
            var count = doc["count"].AsInt32;
            var fallbackTitle = doc.Contains("title") && !doc["title"].IsBsonNull ? doc["title"].AsString : "Sách không xác định";

            bookMap.TryGetValue(bookId, out var book);

            vm.Items.Add(new TopBookItemVM
            {
                BookId = bookId,
                BookTitle = book?.Title ?? fallbackTitle,
                Authors = book != null ? string.Join(", ", book.Authors) : "---",
                CategoryNames = book != null ? book.CategoryIds.Where(id => categoryMap.ContainsKey(id)).Select(id => categoryMap[id]).ToList() : new(),
                BorrowCount = count,
                TotalQuantity = book?.TotalQuantity ?? 0,
                AvailableQuantity = book?.AvailableQuantity ?? 0
            });
        }

        return vm;
    }

    /// <summary>
    /// Báo cáo 2: Danh sách phiếu mượn quá hạn tính đến hiện tại và tiền phạt dự kiến.
    /// Match: Status == Borrowing và DueDate < StartOfTodayVnUtc
    /// </summary>
    public async Task<OverdueReportVM> GetOverdueLoansAsync()
    {
        var vm = new OverdueReportVM();
        var todayUtc = DateHelper.StartOfTodayVnUtc();

        var overdueLoans = await _db.Loans
            .Find(l => l.Status == LoanStatus.Borrowing && l.DueDate < todayUtc)
            .SortBy(l => l.DueDate)
            .ToListAsync();

        if (overdueLoans.Count == 0) return vm;

        var readerIds = overdueLoans.Select(l => l.Reader.ReaderId).Distinct().ToList();
        var readers = await _db.Readers.Find(r => readerIds.Contains(r.Id!)).ToListAsync();
        var readerMap = readers.ToDictionary(r => r.Id!);

        foreach (var loan in overdueLoans)
        {
            var overdueDays = DateHelper.DaysOverdue(loan.DueDate);
            var overdueBooksCount = loan.Items.Count(i => i.ReturnDate == null);
            // Phạt theo QĐ05: 5.000đ / ngày / cuốn
            var estimatedFine = 5000L * overdueDays * overdueBooksCount;

            readerMap.TryGetValue(loan.Reader.ReaderId, out var reader);

            vm.Items.Add(new OverdueLoanItemVM
            {
                LoanId = loan.Id ?? string.Empty,
                LoanCode = loan.LoanCode,
                ReaderId = loan.Reader.ReaderId,
                CardNumber = loan.Reader.CardNumber,
                ReaderName = loan.Reader.FullName,
                ReaderPhone = reader?.Phone,
                BorrowDate = loan.BorrowDate,
                DueDate = loan.DueDate,
                OverdueDays = overdueDays,
                OverdueBooksCount = overdueBooksCount,
                BookTitles = loan.Items.Where(i => i.ReturnDate == null).Select(i => i.Title).ToList(),
                EstimatedFine = estimatedFine
            });
        }

        return vm;
    }

    /// <summary>
    /// Báo cáo 3: Lượt mượn qua các tháng trong năm (Vẽ biểu đồ đường Chart.js).
    /// Pipeline: $match theo năm -> $group theo $month (timezone "Asia/Ho_Chi_Minh") -> $sort
    /// </summary>
    public async Task<BorrowByMonthReportVM> GetBorrowByMonthAsync(int? year)
    {
        var currentYear = DateHelper.TodayVn().Year;
        var selectedYear = year ?? currentYear;

        var vm = new BorrowByMonthReportVM
        {
            SelectedYear = selectedYear,
            AvailableYears = new List<int> { currentYear - 2, currentYear - 1, currentYear, currentYear + 1 }
        };

        var startOfYearUtc = DateHelper.VnDateToUtc(new DateTime(selectedYear, 1, 1));
        var endOfYearUtc = DateHelper.VnDateToUtc(new DateTime(selectedYear, 12, 31, 23, 59, 59));

        var pipeline = new List<BsonDocument>
        {
            // 1. $match các phiếu trong năm
            new BsonDocument("$match", new BsonDocument("BorrowDate", new BsonDocument
            {
                { "$gte", BsonDateTime.Create(startOfYearUtc) },
                { "$lte", BsonDateTime.Create(endOfYearUtc) }
            })),

            // 2. $group theo tháng với timezone "Asia/Ho_Chi_Minh" để không lệch ngày
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$month", new BsonDocument
                {
                    { "date", "$BorrowDate" },
                    { "timezone", "Asia/Ho_Chi_Minh" }
                }) },
                { "loanCount", new BsonDocument("$sum", 1) },
                { "bookCount", new BsonDocument("$sum", new BsonDocument("$size", "$Items")) }
            }),

            // 3. $sort theo tháng
            new BsonDocument("$sort", new BsonDocument("_id", 1))
        };

        var results = await _db.Loans.Aggregate<BsonDocument>(pipeline).ToListAsync();
        var resultMap = results.ToDictionary(
            d => d["_id"].AsInt32,
            d => (LoanCount: d["loanCount"].AsInt32, BookCount: d["bookCount"].AsInt32)
        );

        // Khởi tạo đầy đủ 12 tháng để biểu đồ liền mạch
        for (int m = 1; m <= 12; m++)
        {
            resultMap.TryGetValue(m, out var data);
            vm.Months.Add(new MonthStatVM
            {
                Month = m,
                LoanCount = data.LoanCount,
                BookCount = data.BookCount
            });
        }

        return vm;
    }

    /// <summary>
    /// Báo cáo 4: Lượt mượn theo thể loại sách (Vẽ biểu đồ tròn Chart.js).
    /// Pipeline: $match ngày -> $unwind: "$Items" -> $unwind: "$Items.CategoryIds" -> $group theo categoryId
    /// </summary>
    public async Task<BorrowByCategoryReportVM> GetBorrowByCategoryAsync(DateTime? fromDate, DateTime? toDate)
    {
        var vm = new BorrowByCategoryReportVM
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            vm.ErrorMessage = "Ngày bắt đầu không được lớn hơn ngày kết thúc.";
            return vm;
        }

        var pipeline = new List<BsonDocument>();

        // 1. $match ngày mượn
        if (fromDate.HasValue || toDate.HasValue)
        {
            var dateFilter = new BsonDocument();
            if (fromDate.HasValue)
                dateFilter.Add("$gte", BsonDateTime.Create(DateHelper.VnDateToUtc(fromDate.Value)));
            if (toDate.HasValue)
                dateFilter.Add("$lte", BsonDateTime.Create(DateHelper.VnDateToUtc(toDate.Value.AddDays(1).AddSeconds(-1))));
            pipeline.Add(new BsonDocument("$match", new BsonDocument("BorrowDate", dateFilter)));
        }

        // 2. $unwind Items và CategoryIds
        pipeline.Add(new BsonDocument("$unwind", "$Items"));
        pipeline.Add(new BsonDocument("$unwind", "$Items.CategoryIds"));

        // 3. $group theo CategoryId
        pipeline.Add(new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$Items.CategoryIds" },
            { "borrowCount", new BsonDocument("$sum", 1) }
        }));

        pipeline.Add(new BsonDocument("$sort", new BsonDocument("borrowCount", -1)));

        var results = await _db.Loans.Aggregate<BsonDocument>(pipeline).ToListAsync();
        if (results.Count == 0) return vm;

        var catIds = results.Select(d => d["_id"].AsString).ToList();
        var categories = await _db.Categories.Find(c => catIds.Contains(c.Id!)).ToListAsync();
        var catMap = categories.ToDictionary(c => c.Id!, c => c.Name);

        var total = results.Sum(d => d["borrowCount"].AsInt32);

        foreach (var doc in results)
        {
            var catId = doc["_id"].AsString;
            var count = doc["borrowCount"].AsInt32;
            catMap.TryGetValue(catId, out var name);

            vm.Categories.Add(new CategoryStatVM
            {
                CategoryId = catId,
                CategoryName = name ?? "Thể loại khác",
                BorrowCount = count,
                Percentage = total > 0 ? Math.Round((double)count * 100 / total, 1) : 0
            });
        }

        return vm;
    }

    /// <summary>
    /// Báo cáo 5: Thống kê thu tiền phạt (phạt trễ hạn, làm hư hỏng, làm mất sách).
    /// </summary>
    public async Task<FinesReportVM> GetFinesAsync(DateTime? fromDate, DateTime? toDate)
    {
        var vm = new FinesReportVM
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            vm.ErrorMessage = "Ngày bắt đầu không được lớn hơn ngày kết thúc.";
            return vm;
        }

        var filterBuilder = Builders<Loan>.Filter;
        var filter = filterBuilder.Gt(l => l.TotalFine, 0);

        if (fromDate.HasValue)
            filter &= filterBuilder.Gte(l => l.BorrowDate, DateHelper.VnDateToUtc(fromDate.Value));

        if (toDate.HasValue)
            filter &= filterBuilder.Lte(l => l.BorrowDate, DateHelper.VnDateToUtc(toDate.Value.AddDays(1).AddSeconds(-1)));

        var loans = await _db.Loans.Find(filter)
            .SortByDescending(l => l.BorrowDate)
            .ToListAsync();

        foreach (var loan in loans)
        {
            foreach (var item in loan.Items.Where(i => i.Fine > 0))
            {
                var fineType = item.Condition switch
                {
                    BookCondition.Damaged => "Hư hỏng sách",
                    BookCondition.Lost => "Làm mất sách",
                    _ => "Trễ hạn trả sách"
                };

                vm.Items.Add(new FineDetailVM
                {
                    LoanCode = loan.LoanCode,
                    CardNumber = loan.Reader.CardNumber,
                    ReaderName = loan.Reader.FullName,
                    BookTitle = item.Title,
                    ReturnDate = item.ReturnDate ?? loan.DueDate,
                    Condition = item.Condition,
                    FineAmount = item.Fine,
                    FineType = fineType
                });
            }
        }

        return vm;
    }

    #endregion
}
