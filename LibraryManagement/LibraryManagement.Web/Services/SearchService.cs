using LibraryManagement.Web.Data;

namespace LibraryManagement.Web.Services;

// UC11: tìm kiếm của Thủ thư (sách, độc giả, phiếu mượn). (B viết ở đợt 3)
public class SearchService
{
    private readonly MongoDbContext _db;

    public SearchService(MongoDbContext db) => _db = db;
}
