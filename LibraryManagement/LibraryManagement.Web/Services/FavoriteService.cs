using MongoDB.Bson;
using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.ViewModels.Favorites;

namespace LibraryManagement.Web.Services;

/// <summary>
/// Sách yêu thích của độc giả. Lưu ở mảng readers.favoriteBookIds (nhúng trong hồ sơ độc giả).
/// Chỉ đánh dấu để tiện xem lại; việc mượn vẫn đi qua Đặt giữ / Đặt trước (MyReservations) như cũ.
/// </summary>
public class FavoriteService
{
    /// <summary>Số sách yêu thích tối đa của một độc giả (giữ mảng nhúng luôn nhỏ).</summary>
    public const int MaxFavorites = 50;

    private readonly MongoDbContext _db;

    public FavoriteService(MongoDbContext db) => _db = db;

    private static bool ValidId(string? id) => ObjectId.TryParse(id, out _);

    // Đọc cả hồ sơ (nhỏ) thay vì project riêng mảng: document cũ chưa có field vẫn ra danh sách rỗng
    private async Task<List<string>> LoadIdsAsync(string readerId) =>
        (await _db.Readers.Find(r => r.Id == readerId).FirstOrDefaultAsync())?.FavoriteBookIds ?? new List<string>();

    /// <summary>Id các sách yêu thích của độc giả (để tô trái tim trên trang chủ / trang chi tiết).</summary>
    public async Task<HashSet<string>> GetIdsAsync(string readerId)
    {
        if (!ValidId(readerId)) return new HashSet<string>();
        return new HashSet<string>(await LoadIdsAsync(readerId));
    }

    public async Task<bool> IsFavoriteAsync(string readerId, string bookId)
    {
        if (!ValidId(readerId) || !ValidId(bookId)) return false;
        return await _db.Readers.Find(Builders<Reader>.Filter.And(
            Builders<Reader>.Filter.Eq(r => r.Id, readerId),
            Builders<Reader>.Filter.AnyEq(r => r.FavoriteBookIds, bookId))).AnyAsync();
    }

    /// <summary>
    /// Thêm vào yêu thích. Một lệnh update nguyên tử: $addToSet không tạo trùng, điều kiện
    /// "favoriteBookIds.{Max-1} không tồn tại" chặn vượt giới hạn mà không cần đọc trước.
    /// </summary>
    public async Task<ServiceResult> AddAsync(string readerId, string bookId)
    {
        if (!ValidId(readerId) || !ValidId(bookId) || !await _db.Books.Find(b => b.Id == bookId).AnyAsync())
            return ServiceResult.Fail("Không tìm thấy cuốn sách này.");

        var f = Builders<Reader>.Filter;
        var notFull = f.Exists($"favoriteBookIds.{MaxFavorites - 1}", false);
        var alreadyIn = f.AnyEq(r => r.FavoriteBookIds, bookId);

        var result = await _db.Readers.UpdateOneAsync(
            f.Eq(r => r.Id, readerId) & (notFull | alreadyIn),
            Builders<Reader>.Update.AddToSet(r => r.FavoriteBookIds, bookId));

        if (result.MatchedCount == 0)
        {
            return await _db.Readers.Find(r => r.Id == readerId).AnyAsync()
                ? ServiceResult.Fail($"Bạn chỉ lưu được tối đa {MaxFavorites} sách yêu thích. Hãy bỏ bớt sách cũ rồi thêm lại.")
                : ServiceResult.Fail("Không tìm thấy hồ sơ độc giả của bạn.");
        }
        return ServiceResult.Ok("Đã thêm vào sách yêu thích.");
    }

    /// <summary>Bỏ khỏi yêu thích ($pull). Bỏ sách không có trong danh sách cũng coi là thành công.</summary>
    public async Task<ServiceResult> RemoveAsync(string readerId, string bookId)
    {
        if (!ValidId(readerId) || !ValidId(bookId))
            return ServiceResult.Fail("Không tìm thấy cuốn sách này.");

        await _db.Readers.UpdateOneAsync(r => r.Id == readerId,
            Builders<Reader>.Update.Pull(r => r.FavoriteBookIds, bookId));
        return ServiceResult.Ok("Đã bỏ khỏi sách yêu thích.");
    }

    /// <summary>
    /// Danh sách cho trang "Sách yêu thích": mới thêm lên đầu, kèm tình trạng kho và yêu cầu đặt còn hiệu lực.
    /// Sách đã bị xóa khỏi kho thì bỏ luôn khỏi mảng của độc giả.
    /// </summary>
    public async Task<List<FavoriteBookVM>> GetListAsync(string readerId)
    {
        if (!ValidId(readerId)) return new List<FavoriteBookVM>();
        var ids = await LoadIdsAsync(readerId);
        if (ids.Count == 0) return new List<FavoriteBookVM>();

        var books = (await _db.Books.Find(Builders<Book>.Filter.In(b => b.Id, ids)).ToListAsync())
            .ToDictionary(b => b.Id!);

        var missing = ids.Where(id => !books.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            await _db.Readers.UpdateOneAsync(r => r.Id == readerId,
                Builders<Reader>.Update.PullAll(r => r.FavoriteBookIds, missing));

        var categoryIds = books.Values.SelectMany(b => b.CategoryIds).Distinct().ToList();
        var categoryMap = (await _db.Categories.Find(Builders<Category>.Filter.In(c => c.Id, categoryIds)).ToListAsync())
            .ToDictionary(c => c.Id!, c => c.Name);

        // Yêu cầu còn hiệu lực theo QĐ08: Chờ duyệt / Đã duyệt / Đang giữ.
        // Bản giữ đã quá ngày nhận (holdUntil trước hôm nay) không tính, dù tiến trình nền chưa kịp hủy (BUG-08).
        var bookIds = books.Keys.ToList();
        var startOfToday = DateHelper.StartOfTodayVnUtc();
        var active = await _db.Reservations.Find(r =>
                r.ReaderId == readerId && bookIds.Contains(r.BookId) &&
                (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved
                 || (r.Status == ReservationStatus.Held && r.HoldUntil >= startOfToday)))
            .ToListAsync();
        var activeMap = active.GroupBy(r => r.BookId).ToDictionary(g => g.Key, g => g.First().Status);

        return Enumerable.Reverse(ids)
            .Where(books.ContainsKey)
            .Select(id =>
            {
                var b = books[id];
                return new FavoriteBookVM
                {
                    BookId = id,
                    Title = b.Title,
                    Authors = string.Join(", ", b.Authors),
                    FirstCategory = b.CategoryIds.Where(categoryMap.ContainsKey).Select(c => categoryMap[c]).FirstOrDefault(),
                    CoverImageId = b.CoverImageId,
                    AvailableQuantity = b.AvailableQuantity,
                    TotalQuantity = b.TotalQuantity,
                    ActiveReservation = activeMap.TryGetValue(id, out var s) ? s : null
                };
            })
            .ToList();
    }
}
