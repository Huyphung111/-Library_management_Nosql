using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
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
            // Sửa (A, 22/11/2026): thoát ký tự đặc biệt để "(" không làm lỗi regex; tìm được theo một phần tên tác giả
            var regex = new MongoDB.Bson.BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
            filter &= filterBuilder.Or(
                filterBuilder.Regex(b => b.Title, regex),
                filterBuilder.Regex("authors", regex)
            );
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            if (!ValidId(categoryId)) return new List<BookListVM>();
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
            AvailableQuantity = b.AvailableQuantity,
            CoverImageId = b.CoverImageId
        }).ToList();
    }

    /// <summary>
    /// Lấy chi tiết sách theo Id.
    /// </summary>
    public async Task<Book?> GetByIdAsync(string id) =>
        ValidId(id) ? await _db.Books.Find(b => b.Id == id).FirstOrDefaultAsync() : null;

    // Sửa lỗi 500 khi id sai định dạng ObjectId trên URL (A, 22/11/2026)
    private static bool ValidId(string? id) => MongoDB.Bson.ObjectId.TryParse(id, out _);


    /// <summary>
    /// Thêm sách mới.
    /// </summary>
    public async Task<ServiceResult> CreateAsync(BookFormVM vm)
    {
        var coverError = ValidateCover(vm.CoverFile);
        if (coverError != null) return ServiceResult.Fail(coverError, nameof(BookFormVM.CoverFile));

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

        // Ảnh bìa: upload vào GridFS sau khi có Id sách (lưu bookId trong metadata)
        if (vm.CoverFile != null)
        {
            var coverId = await UploadCoverAsync(vm.CoverFile, book.Id!);
            await _db.Books.UpdateOneAsync(b => b.Id == book.Id, Builders<Book>.Update.Set(b => b.CoverImageId, coverId));
        }
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

        var coverError = ValidateCover(vm.CoverFile);
        if (coverError != null) return ServiceResult.Fail(coverError, nameof(BookFormVM.CoverFile));

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

        // Tính lại số còn lại theo độ chênh lệch.
        // Sửa (A, 22/11/2026): dùng $inc + điều kiện "availableQuantity >= -delta" trong filter (mục 7.2),
        // để không lệch khi đúng lúc đó có người mượn / trả sách.
        var delta = vm.TotalQuantity - existingBook.TotalQuantity;

        var update = Builders<Book>.Update
            .Set(b => b.Title, vm.Title.Trim())
            .Set(b => b.Authors, authors)
            .Set(b => b.Publisher, string.IsNullOrWhiteSpace(vm.Publisher) ? null : vm.Publisher.Trim())
            .Set(b => b.PublishYear, vm.PublishYear)
            .Set(b => b.Price, vm.Price)
            .Set(b => b.CategoryIds, vm.SelectedCategoryIds)
            .Inc(b => b.TotalQuantity, delta)
            .Inc(b => b.AvailableQuantity, delta);

        var minAvailable = Math.Max(0, -delta);
        var result = await _db.Books.UpdateOneAsync(b => b.Id == id && b.AvailableQuantity >= minAvailable, update);
        if (result.MatchedCount == 0)
            return ServiceResult.Fail("Số sách đang cho mượn vừa thay đổi nên không giảm được số lượng tổng. Vui lòng tải lại và thử lại.",
                nameof(BookFormVM.TotalQuantity));

        // Ảnh bìa: có ảnh mới thì thay, tick "Xóa ảnh" thì bỏ; ảnh cũ xóa khỏi GridFS sau khi đã cập nhật sách
        if (vm.CoverFile != null || vm.RemoveCover)
        {
            var newCoverId = vm.CoverFile != null ? await UploadCoverAsync(vm.CoverFile, id) : null;
            await _db.Books.UpdateOneAsync(b => b.Id == id, Builders<Book>.Update.Set(b => b.CoverImageId, newCoverId));
            await DeleteCoverAsync(existingBook.CoverImageId);
        }
        return ServiceResult.Ok("Cập nhật sách thành công.");
    }

    /// <summary>
    /// Xóa sách. Tuân thủ QĐ09: Chỉ xóa khi không có phiếu mượn đang mở chứa sách này.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(string id)
    {
        if (!ValidId(id)) return ServiceResult.Fail("Không tìm thấy sách cần xóa.");
        var hasOpenLoan = await _loanService.HasOpenLoanForBookAsync(id);
        if (hasOpenLoan)
            return ServiceResult.Fail("Không thể xóa sách này vì đang nằm trong phiếu mượn chưa trả.");

        var book = await GetByIdAsync(id);
        var result = await _db.Books.DeleteOneAsync(b => b.Id == id);
        if (result.DeletedCount == 0)
            return ServiceResult.Fail("Không tìm thấy sách cần xóa.");

        await DeleteCoverAsync(book?.CoverImageId);   // xóa luôn ảnh bìa trong GridFS
        return ServiceResult.Ok("Xóa sách thành công.");
    }

    // ------------------------------------------------------------------
    // Ảnh bìa bằng GridFS (mục 7.5 thiết kế kỹ thuật)
    // ------------------------------------------------------------------

    public const long MaxCoverBytes = 2 * 1024 * 1024;
    private static readonly string[] CoverExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    /// <summary>Kiểm tra ảnh bìa: đúng đuôi, đúng ContentType, đúng chữ ký file (vài byte đầu), không quá 2 MB. Null nếu hợp lệ hoặc không chọn ảnh.</summary>
    public static string? ValidateCover(Microsoft.AspNetCore.Http.IFormFile? file)
    {
        if (file == null) return null;
        if (file.Length == 0) return "File ảnh rỗng.";
        if (file.Length > MaxCoverBytes) return "Ảnh bìa tối đa 2 MB.";
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!CoverExtensions.Contains(ext) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return "Chỉ nhận ảnh JPG, PNG hoặc WebP.";
        using var stream = file.OpenReadStream();
        if (DetectImageType(stream) == null) return "File không phải ảnh JPG, PNG hoặc WebP hợp lệ.";
        return null;
    }

    /// <summary>Đọc chữ ký file để biết đúng là ảnh, không tin đuôi file. Trả về content type hoặc null.</summary>
    private static string? DetectImageType(Stream stream)
    {
        var head = new byte[12];
        var n = stream.Read(head, 0, head.Length);
        if (n >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return "image/jpeg";
        if (n >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47) return "image/png";
        if (n >= 12 && head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F'
            && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P') return "image/webp";
        return null;
    }

    /// <summary>Upload ảnh vào GridFS bucket "covers", metadata ghi contentType và bookId. Trả về id file.</summary>
    private async Task<string> UploadCoverAsync(Microsoft.AspNetCore.Http.IFormFile file, string bookId)
    {
        await using var stream = file.OpenReadStream();
        var contentType = DetectImageType(stream)!;
        stream.Position = 0;
        return await UploadCoverStreamAsync(stream, Path.GetFileName(file.FileName), contentType, bookId, _db.Covers);
    }

    /// <summary>Dùng chung cho form và seed (DbInitializer).</summary>
    public static async Task<string> UploadCoverStreamAsync(Stream stream, string fileName, string contentType, string bookId, GridFSBucket bucket)
    {
        var options = new GridFSUploadOptions
        {
            Metadata = new BsonDocument { { "contentType", contentType }, { "bookId", ObjectId.Parse(bookId) } }
        };
        var id = await bucket.UploadFromStreamAsync(fileName, stream, options);
        return id.ToString();
    }

    private async Task DeleteCoverAsync(string? coverId)
    {
        if (!ObjectId.TryParse(coverId, out var oid)) return;
        try { await _db.Covers.DeleteAsync(oid); }
        catch (GridFSFileNotFoundException) { /* đã bị xóa trước đó */ }
    }

    /// <summary>Mở ảnh bìa để trả về trình duyệt. Null nếu id sai hoặc không có file.</summary>
    public async Task<(Stream Stream, string ContentType)?> OpenCoverAsync(string? coverId)
    {
        if (!ObjectId.TryParse(coverId, out var oid)) return null;
        try
        {
            var stream = await _db.Covers.OpenDownloadStreamAsync(oid);
            var contentType = stream.FileInfo.Metadata?.GetValue("contentType", "image/jpeg").AsString ?? "image/jpeg";
            return (stream, contentType);
        }
        catch (GridFSFileNotFoundException)
        {
            return null;
        }
    }
}
