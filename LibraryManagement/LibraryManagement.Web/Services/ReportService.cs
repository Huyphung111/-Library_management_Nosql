using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Options;
using LibraryManagement.Web.ViewModels.Reports;

namespace LibraryManagement.Web.Services;

/// <summary>
/// UC12: Báo cáo, thống kê và số liệu tổng quan Dashboard (Member B).
/// Sử dụng các MongoDB Aggregation Pipeline chuyên sâu theo thiết kế kỹ thuật mục 7.4.
/// </summary>
public class ReportService
{
    private readonly MongoDbContext _db;
    private readonly LibraryRules _rules;

    public ReportService(MongoDbContext db, IOptions<LibraryRules> rules)
    {
        _db = db;
        _rules = rules.Value;
    }

    private static string BsonIdToString(BsonValue val)
    {
        if (val == null || val.IsBsonNull) return string.Empty;
        if (val.IsObjectId) return val.AsObjectId.ToString();
        if (val.IsString) return val.AsString;
        return val.ToString() ?? string.Empty;
    }

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

        // 8. Sách đang giữ chờ độc giả đến nhận (đặt giữ online). Bỏ qua bản đã quá ngày nhận
        //    (HoldExpiryService sẽ trả về kho trong vài phút); đếm riêng số cuốn hôm nay là ngày nhận cuối.
        var tomorrowUtc = todayUtc.AddDays(1);
        var holdUntilList = await _db.Reservations
            .Find(r => r.Status == ReservationStatus.Held && r.HoldUntil >= todayUtc)
            .Project(r => r.HoldUntil)
            .ToListAsync();
        vm.HeldReservations = holdUntilList.Count;
        vm.HeldExpiringToday = holdUntilList.Count(d => d < tomorrowUtc);

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
                dateFilter.Add("$lt", BsonDateTime.Create(DateHelper.VnDateToUtc(toDate.Value.AddDays(1))));   // trước 00:00 ngày hôm sau
            matchFilter.Add("borrowDate", dateFilter);
        }

        if (matchFilter.ElementCount > 0)
        {
            pipeline.Add(new BsonDocument("$match", matchFilter));
        }

        // 2. $unwind items
        pipeline.Add(new BsonDocument("$unwind", "$items"));

        // 3. $group theo items.bookId
        pipeline.Add(new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$items.bookId" },
            { "count", new BsonDocument("$sum", 1) },
            { "title", new BsonDocument("$first", "$items.title") }
        }));

        // 4. $sort giảm dần và $limit 10
        pipeline.Add(new BsonDocument("$sort", new BsonDocument("count", -1)));
        pipeline.Add(new BsonDocument("$limit", 10));

        var aggResults = await _db.Loans.Aggregate<BsonDocument>(pipeline).ToListAsync();
        if (aggResults.Count == 0) return vm;

        var bookIds = aggResults.Select(d => BsonIdToString(d["_id"])).ToList();
        var books = await _db.Books.Find(b => bookIds.Contains(b.Id!)).ToListAsync();
        var bookMap = books.ToDictionary(b => b.Id!);

        var categories = await _db.Categories.Find(_ => true).ToListAsync();
        var categoryMap = categories.ToDictionary(c => c.Id!, c => c.Name);

        foreach (var doc in aggResults)
        {
            var bookId = BsonIdToString(doc["_id"]);
            var count = doc["count"].ToInt32();
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
            // Sửa (A, 24/11/2026) BUG-01. Phạt theo QĐ05: FinePerDay / ngày / cuốn, đọc từ LibraryRules (BUG-01: không viết cứng 5000)
            var estimatedFine = _rules.FinePerDay * overdueDays * overdueBooksCount;

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
        var startOfNextYearUtc = DateHelper.VnDateToUtc(new DateTime(selectedYear + 1, 1, 1));

        var pipeline = new List<BsonDocument>
        {
            // 1. $match các phiếu trong năm
            new BsonDocument("$match", new BsonDocument("borrowDate", new BsonDocument
            {
                { "$gte", BsonDateTime.Create(startOfYearUtc) },
                { "$lt", BsonDateTime.Create(startOfNextYearUtc) }
            })),

            // 2. $group theo tháng với timezone "Asia/Ho_Chi_Minh" để không lệch ngày
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$month", new BsonDocument
                {
                    { "date", "$borrowDate" },
                    { "timezone", "Asia/Ho_Chi_Minh" }
                }) },
                { "loanCount", new BsonDocument("$sum", 1) },
                { "bookCount", new BsonDocument("$sum", new BsonDocument("$size", "$items")) }
            }),

            // 3. $sort theo tháng
            new BsonDocument("$sort", new BsonDocument("_id", 1))
        };

        var results = await _db.Loans.Aggregate<BsonDocument>(pipeline).ToListAsync();
        var resultMap = results.ToDictionary(
            d => d["_id"].ToInt32(),
            d => (LoanCount: d["loanCount"].ToInt32(), BookCount: d["bookCount"].ToInt32())
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
                dateFilter.Add("$lt", BsonDateTime.Create(DateHelper.VnDateToUtc(toDate.Value.AddDays(1))));   // trước 00:00 ngày hôm sau
            pipeline.Add(new BsonDocument("$match", new BsonDocument("borrowDate", dateFilter)));
        }

        // 2. $unwind items và categoryIds
        pipeline.Add(new BsonDocument("$unwind", "$items"));
        pipeline.Add(new BsonDocument("$unwind", "$items.categoryIds"));

        // 3. $group theo categoryIds
        pipeline.Add(new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$items.categoryIds" },
            { "borrowCount", new BsonDocument("$sum", 1) }
        }));

        pipeline.Add(new BsonDocument("$sort", new BsonDocument("borrowCount", -1)));

        var results = await _db.Loans.Aggregate<BsonDocument>(pipeline).ToListAsync();
        if (results.Count == 0) return vm;

        var catIds = results.Select(d => BsonIdToString(d["_id"])).ToList();
        var categories = await _db.Categories.Find(c => catIds.Contains(c.Id!)).ToListAsync();
        var catMap = categories.ToDictionary(c => c.Id!, c => c.Name);

        var total = results.Sum(d => d["borrowCount"].ToInt32());

        foreach (var doc in results)
        {
            var catId = BsonIdToString(doc["_id"]);
            var count = doc["borrowCount"].ToInt32();
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
    /// Báo cáo 5: Thống kê thu tiền phạt (phạt trễ hạn, làm hư hỏng, làm mất sách), lọc theo ngày trả sách.
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

        // Sửa (A, 24/11/2026) BUG-02. UC12: tiền phạt tính theo NGÀY TRẢ của từng cuốn (lúc phát sinh phạt), không theo ngày mượn (BUG-02).
        // returnDate là thời điểm (UtcNow), nên mốc cuối là "trước 00:00 giờ VN của ngày hôm sau".
        var itemFilter = new BsonDocument("fine", new BsonDocument("$gt", 0));
        if (fromDate.HasValue || toDate.HasValue)
        {
            var range = new BsonDocument();
            if (fromDate.HasValue)
                range.Add("$gte", BsonDateTime.Create(DateHelper.VnDateToUtc(fromDate.Value)));
            if (toDate.HasValue)
                range.Add("$lt", BsonDateTime.Create(DateHelper.VnDateToUtc(toDate.Value.AddDays(1))));
            itemFilter.Add("returnDate", range);
        }

        // Pipeline: $match phiếu có cuốn thỏa điều kiện -> $unwind items -> $match lại từng cuốn -> $sort ngày trả giảm dần -> $project
        var itemMatch = new BsonDocument(itemFilter.Select(e => new BsonElement("items." + e.Name, e.Value)));
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("items", new BsonDocument("$elemMatch", itemFilter))),
            new BsonDocument("$unwind", "$items"),
            new BsonDocument("$match", itemMatch),
            new BsonDocument("$sort", new BsonDocument("items.returnDate", -1)),
            new BsonDocument("$project", new BsonDocument
            {
                { "_id", 0 },
                { "loanCode", 1 },
                { "cardNumber", "$reader.cardNumber" },
                { "readerName", "$reader.fullName" },
                { "title", "$items.title" },
                { "returnDate", "$items.returnDate" },
                { "condition", "$items.condition" },
                { "fine", "$items.fine" }
            })
        };

        var rows = await _db.Loans.Aggregate<BsonDocument>(pipeline).ToListAsync();
        foreach (var row in rows)
        {
            BookCondition? condition = row.GetValue("condition", BsonNull.Value) is { IsString: true } c
                && Enum.TryParse<BookCondition>(c.AsString, out var parsed) ? parsed : null;

            vm.Items.Add(new FineDetailVM
            {
                LoanCode = row["loanCode"].AsString,
                CardNumber = row["cardNumber"].AsString,
                ReaderName = row["readerName"].AsString,
                BookTitle = row["title"].AsString,
                ReturnDate = row["returnDate"].ToUniversalTime(),
                Condition = condition,
                FineAmount = row["fine"].ToInt64(),
                FineType = condition switch
                {
                    BookCondition.Damaged => "Hư hỏng sách",
                    BookCondition.Lost => "Làm mất sách",
                    _ => "Trễ hạn trả sách"
                }
            });
        }

        return vm;
    }

    #endregion
}
