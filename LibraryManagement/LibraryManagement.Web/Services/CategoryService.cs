using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.ViewModels.Categories;

namespace LibraryManagement.Web.Services;

/// <summary>
/// UC05: Quản lý thể loại sách (Member B).
/// </summary>
public class CategoryService
{
    private readonly MongoDbContext _db;

    public CategoryService(MongoDbContext db) => _db = db;

    /// <summary>
    /// Lấy danh sách toàn bộ thể loại kèm số đầu sách của mỗi thể loại.
    /// </summary>
    public async Task<List<CategoryListVM>> GetAllWithBookCountAsync()
    {
        var categories = await _db.Categories.Find(_ => true)
            .SortBy(c => c.Name)
            .ToListAsync();

        // Lấy danh sách categoryIds từ tất cả sách để tính số đầu sách mỗi thể loại
        var allCategoryLists = await _db.Books.Find(_ => true)
            .Project(b => b.CategoryIds)
            .ToListAsync();

        var countDict = allCategoryLists
            .SelectMany(list => list)
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        return categories.Select(c => new CategoryListVM
        {
            Id = c.Id!,
            Name = c.Name,
            Description = c.Description,
            BookCount = countDict.GetValueOrDefault(c.Id!, 0)
        }).ToList();
    }

    /// <summary>
    /// Lấy chi tiết một thể loại theo Id.
    /// </summary>
    public async Task<Category?> GetByIdAsync(string id) =>
        ValidId(id) ? await _db.Categories.Find(c => c.Id == id).FirstOrDefaultAsync() : null;

    // Sửa lỗi 500 khi id sai định dạng ObjectId trên URL (A, 22/11/2026)
    private static bool ValidId(string? id) => MongoDB.Bson.ObjectId.TryParse(id, out _);


    /// <summary>
    /// Thêm thể loại mới. Bắt lỗi trùng tên từ unique index.
    /// </summary>
    public async Task<ServiceResult> CreateAsync(CategoryFormVM vm)
    {
        var category = new Category
        {
            Name = vm.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim()
        };

        try
        {
            await _db.Categories.InsertOneAsync(category);
            return ServiceResult.Ok("Thêm thể loại thành công.");
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return ServiceResult.Fail("Tên thể loại đã tồn tại.", nameof(CategoryFormVM.Name));
        }
    }

    /// <summary>
    /// Cập nhật tên hoặc mô tả thể loại.
    /// </summary>
    public async Task<ServiceResult> UpdateAsync(string id, CategoryFormVM vm)
    {
        if (!ValidId(id)) return ServiceResult.Fail("Không tìm thấy thể loại cần cập nhật.");
        var update = Builders<Category>.Update
            .Set(c => c.Name, vm.Name.Trim())
            .Set(c => c.Description, string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim());

        try
        {
            var result = await _db.Categories.UpdateOneAsync(c => c.Id == id, update);
            if (result.MatchedCount == 0)
                return ServiceResult.Fail("Không tìm thấy thể loại cần cập nhật.");

            return ServiceResult.Ok("Cập nhật thể loại thành công.");
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return ServiceResult.Fail("Tên thể loại đã tồn tại.", nameof(CategoryFormVM.Name));
        }
    }

    /// <summary>
    /// Xóa thể loại. Tuân thủ QĐ09: chỉ xóa khi không còn sách thuộc thể loại đó.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(string id)
    {
        if (!ValidId(id)) return ServiceResult.Fail("Không tìm thấy thể loại cần xóa.");
        var bookCount = await _db.Books.CountDocumentsAsync(b => b.CategoryIds.Contains(id));
        if (bookCount > 0)
        {
            return ServiceResult.Fail($"Không thể xóa thể loại này vì còn {bookCount} đầu sách thuộc thể loại.");
        }

        var result = await _db.Categories.DeleteOneAsync(c => c.Id == id);
        if (result.DeletedCount == 0)
            return ServiceResult.Fail("Không tìm thấy thể loại cần xóa.");

        return ServiceResult.Ok("Xóa thể loại thành công.");
    }
}
