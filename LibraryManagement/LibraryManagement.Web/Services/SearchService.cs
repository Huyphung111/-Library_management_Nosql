using MongoDB.Bson;
using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.ViewModels.Search;

namespace LibraryManagement.Web.Services;

/// <summary>
/// UC11: Tìm kiếm đa năng của Thủ thư (Sách, Độc giả, Phiếu mượn).
/// </summary>
public class SearchService
{
    private readonly MongoDbContext _db;

    public SearchService(MongoDbContext db) => _db = db;

    /// <summary>
    /// Lấy danh mục thể loại để hiển thị dropdown bộ lọc.
    /// </summary>
    public async Task<List<Category>> GetCategoriesAsync()
    {
        return await _db.Categories.Find(_ => true).SortBy(c => c.Name).ToListAsync();
    }

    /// <summary>
    /// Tra cứu Sách theo từ khóa (tên, tác giả) và thể loại.
    /// </summary>
    public async Task<List<BookSearchResultVM>> SearchBooksAsync(string? keyword, string? categoryId)
    {
        var filterBuilder = Builders<Book>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var s = keyword.Trim();
            var regex = new BsonRegularExpression(s, "i");
            filter &= filterBuilder.Or(
                filterBuilder.Regex(b => b.Title, regex),
                filterBuilder.Regex("authors", regex)
            );
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            filter &= filterBuilder.AnyEq(b => b.CategoryIds, categoryId);
        }

        var books = await _db.Books.Find(filter)
            .SortBy(b => b.Title)
            .ToListAsync();

        var categories = await _db.Categories.Find(_ => true).ToListAsync();
        var categoryMap = categories.ToDictionary(c => c.Id!, c => c.Name);

        return books.Select(b => new BookSearchResultVM
        {
            Id = b.Id ?? string.Empty,
            Title = b.Title,
            Authors = string.Join(", ", b.Authors),
            CategoryNames = b.CategoryIds.Where(id => categoryMap.ContainsKey(id)).Select(id => categoryMap[id]).ToList(),
            Publisher = b.Publisher ?? string.Empty,
            PublishYear = b.PublishYear,
            Price = b.Price,
            TotalQuantity = b.TotalQuantity,
            AvailableQuantity = b.AvailableQuantity
        }).ToList();
    }

    /// <summary>
    /// Tra cứu Độc giả theo Mã thẻ, Họ tên, Số điện thoại hoặc CCCD.
    /// </summary>
    public async Task<List<ReaderSearchResultVM>> SearchReadersAsync(string? keyword)
    {
        var filterBuilder = Builders<Reader>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var s = keyword.Trim();
            var regex = new BsonRegularExpression(s, "i");
            filter &= filterBuilder.Or(
                filterBuilder.Regex(r => r.CardNumber, regex),
                filterBuilder.Regex(r => r.FullName, regex),
                filterBuilder.Regex(r => r.IdNumber, regex),
                filterBuilder.Regex(r => r.Phone, regex)
            );
        }

        var readers = await _db.Readers.Find(filter)
            .SortBy(r => r.CardNumber)
            .ToListAsync();

        return readers.Select(r => new ReaderSearchResultVM
        {
            Id = r.Id ?? string.Empty,
            CardNumber = r.CardNumber,
            FullName = r.FullName,
            IdNumber = r.IdNumber,
            Phone = r.Phone,
            Email = r.Email,
            IssuedDate = r.IssuedDate,
            ExpiryDate = r.ExpiryDate,
            CardStatus = r.CardStatus,
            IsExpired = r.IsExpired,
            LockReason = r.LockReason
        }).ToList();
    }

    /// <summary>
    /// Tra cứu Phiếu mượn theo Mã phiếu, Mã thẻ/Họ tên độc giả, Trạng thái và Khoảng ngày mượn.
    /// </summary>
    public async Task<List<LoanSearchResultVM>> SearchLoansAsync(
        string? keyword, string? loanStatus, DateTime? fromDate, DateTime? toDate)
    {
        var filterBuilder = Builders<Loan>.Filter;
        var filter = filterBuilder.Empty;

        // 1. Tìm theo từ khóa (Mã phiếu, Mã thẻ, Họ tên độc giả)
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var s = keyword.Trim();
            var regex = new BsonRegularExpression(s, "i");
            filter &= filterBuilder.Or(
                filterBuilder.Regex(l => l.LoanCode, regex),
                filterBuilder.Regex(l => l.Reader.CardNumber, regex),
                filterBuilder.Regex(l => l.Reader.FullName, regex)
            );
        }

        // 2. Lọc theo trạng thái
        if (!string.IsNullOrWhiteSpace(loanStatus))
        {
            var st = loanStatus.ToLower();
            if (st == "borrowing")
            {
                filter &= filterBuilder.Eq(l => l.Status, LoanStatus.Borrowing);
            }
            else if (st == "returned")
            {
                filter &= filterBuilder.Eq(l => l.Status, LoanStatus.Returned);
            }
            else if (st == "overdue")
            {
                // Phiếu quá hạn: Đang mượn và DueDate < hôm nay (UTC)
                var todayUtc = DateHelper.StartOfTodayVnUtc();
                filter &= filterBuilder.And(
                    filterBuilder.Eq(l => l.Status, LoanStatus.Borrowing),
                    filterBuilder.Lt(l => l.DueDate, todayUtc)
                );
            }
        }

        // 3. Lọc theo khoảng ngày mượn (quy đổi sang UTC)
        if (fromDate.HasValue)
        {
            var fromUtc = DateHelper.VnDateToUtc(fromDate.Value);
            filter &= filterBuilder.Gte(l => l.BorrowDate, fromUtc);
        }

        if (toDate.HasValue)
        {
            // Đến hết ngày toDate (23:59:59)
            var toUtc = DateHelper.VnDateToUtc(toDate.Value.AddDays(1).AddSeconds(-1));
            filter &= filterBuilder.Lte(l => l.BorrowDate, toUtc);
        }

        var loans = await _db.Loans.Find(filter)
            .SortByDescending(l => l.BorrowDate)
            .ToListAsync();

        return loans.Select(l => new LoanSearchResultVM
        {
            Id = l.Id ?? string.Empty,
            LoanCode = l.LoanCode,
            ReaderId = l.Reader.ReaderId,
            CardNumber = l.Reader.CardNumber,
            ReaderName = l.Reader.FullName,
            BorrowDate = l.BorrowDate,
            DueDate = l.DueDate,
            Status = l.Status,
            IsOverdue = l.IsOverdue,
            OverdueDays = l.OverdueDays,
            TotalFine = l.TotalFine,
            BookTitles = l.Items.Select(x => x.Title).ToList(),
            ItemsCount = l.Items.Count,
            ReturnedCount = l.Items.Count(x => x.ReturnDate != null)
        }).ToList();
    }
}
