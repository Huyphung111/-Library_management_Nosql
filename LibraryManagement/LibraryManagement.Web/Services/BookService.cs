using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.ViewModels.Books;

namespace LibraryManagement.Web.Services;

/// <summary>
/// UC04, UC13: Quản lý sách và tìm kiếm sách (Member B).
/// </summary>
public class BookService
{
    private readonly MongoDbContext _db;
    private readonly LoanService _loanService;

    public BookService(MongoDbContext db, LoanService loanService)
    {
        _db = db;
        _loanService = loanService;
    }

    /// <summary>
    /// Lấy danh sách sách, hỗ trợ lọc theo từ khóa và thể loại.
    /// </summary>
    public async Task<List<BookListVM>> GetAllAsync(string? search = null, string? categoryId = null)
    {
        var filterBuilder = Builders<Book>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            // Lọc theo text search hoặc regex trên title/authors
            filter &= filterBuilder.Or(
                filterBuilder.Regex(b => b.Title, new MongoDB.Bson.BsonRegularExpression(s, "i")),
                filterBuilder.AnyEq(b => b.Authors, s)
            );
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            filter &= filterBuilder.AnyEq(b => b.CategoryIds, categoryId);
        }

        var books = await _db.Books.Find(filter)
            .SortByDescending(b => b.CreatedAt)
            .ToListAsync();

        // Lấy danh mục thể loại để map tên
        var categories = await _db.Categories.Find(_ => true).ToListAsync();
        var categoryMap = categories.ToDictionary(c => c.Id!, c => c.Name);

        return books.Select(b => new BookListVM
        {
            Id = b.Id!,
            Title = b.Title,
            Authors = string.Join(", ", b.Authors),
            Publisher = b.Publisher ?? "—",
            PublishYear = b.PublishYear,
            Price = b.Price,
            CategoryNames = b.CategoryIds.Where(id => categoryMap.ContainsKey(id)).Select(id => categoryMap[id]).ToList(),
            TotalQuantity = b.TotalQuantity,
            AvailableQuantity = b.AvailableQuantity
        }).ToList();
    }

    /// <summary>
    /// Lấy chi tiết sách theo Id.
    /// </summary>
    public async Task<Book?> GetByIdAsync(string id) =>
        await _db.Books.Find(b => b.Id == id).FirstOrDefaultAsync();

    /// <summary>
    /// Thêm sách mới.
    /// </summary>
    public async Task<ServiceResult> CreateAsync(BookFormVM vm)
    {
        if (vm.SelectedCategoryIds.Count == 0)
            return ServiceResult.Fail("Vui lòng chọn ít nhất một thể loại.", nameof(BookFormVM.SelectedCategoryIds));

        var authors = vm.AuthorsString
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

        if (authors.Count == 0)
            return ServiceResult.Fail("Vui lòng nhập ít nhất một tác giả.", nameof(BookFormVM.AuthorsString));

        var book = new Book
        {
            Title = vm.Title.Trim(),
            Authors = authors,
            Publisher = string.IsNullOrWhiteSpace(vm.Publisher) ? null : vm.Publisher.Trim(),
            PublishYear = vm.PublishYear,
            Price = vm.Price,
            CategoryIds = vm.SelectedCategoryIds,
            TotalQuantity = vm.TotalQuantity,
            AvailableQuantity = vm.TotalQuantity,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Books.InsertOneAsync(book);
        return ServiceResult.Ok("Thêm sách mới thành công.");
    }

    /// <summary>
    /// Cập nhật sách. Tuân thủ QĐ09: Không giảm tổng số lượng xuống dưới số đang cho mượn.
    /// </summary>
    public async Task<ServiceResult> UpdateAsync(string id, BookFormVM vm)
    {
        var existingBook = await GetByIdAsync(id);
        if (existingBook == null)
            return ServiceResult.Fail("Không tìm thấy sách cần cập nhật.");

        if (vm.SelectedCategoryIds.Count == 0)
            return ServiceResult.Fail("Vui lòng chọn ít nhất một thể loại.", nameof(BookFormVM.SelectedCategoryIds));

        var authors = vm.AuthorsString
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

        if (authors.Count == 0)
            return ServiceResult.Fail("Vui lòng nhập ít nhất một tác giả.", nameof(BookFormVM.AuthorsString));

        // Kiểm tra quy định số lượng đang mượn (QĐ09)
        var currentlyBorrowed = existingBook.TotalQuantity - existingBook.AvailableQuantity;
        if (vm.TotalQuantity < currentlyBorrowed)
        {
            return ServiceResult.Fail(
                $"Số lượng tổng mới không thể nhỏ hơn số sách đang cho mượn ({currentlyBorrowed} cuốn).",
                nameof(BookFormVM.TotalQuantity));
        }

        // Tính lại số còn lại theo độ chênh lệch
        var delta = vm.TotalQuantity - existingBook.TotalQuantity;
        var newAvailable = existingBook.AvailableQuantity + delta;

        var update = Builders<Book>.Update
            .Set(b => b.Title, vm.Title.Trim())
            .Set(b => b.Authors, authors)
            .Set(b => b.Publisher, string.IsNullOrWhiteSpace(vm.Publisher) ? null : vm.Publisher.Trim())
            .Set(b => b.PublishYear, vm.PublishYear)
            .Set(b => b.Price, vm.Price)
            .Set(b => b.CategoryIds, vm.SelectedCategoryIds)
            .Set(b => b.TotalQuantity, vm.TotalQuantity)
            .Set(b => b.AvailableQuantity, newAvailable);

        await _db.Books.UpdateOneAsync(b => b.Id == id, update);
        return ServiceResult.Ok("Cập nhật sách thành công.");
    }

    /// <summary>
    /// Xóa sách. Tuân thủ QĐ09: Chỉ xóa khi không có phiếu mượn đang mở chứa sách này.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(string id)
    {
        var hasOpenLoan = await _loanService.HasOpenLoanForBookAsync(id);
        if (hasOpenLoan)
            return ServiceResult.Fail("Không thể xóa sách này vì đang nằm trong phiếu mượn chưa trả.");

        var result = await _db.Books.DeleteOneAsync(b => b.Id == id);
        if (result.DeletedCount == 0)
            return ServiceResult.Fail("Không tìm thấy sách cần xóa.");

        return ServiceResult.Ok("Xóa sách thành công.");
    }
}
