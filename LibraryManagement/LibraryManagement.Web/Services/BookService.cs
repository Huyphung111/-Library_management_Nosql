using LibraryManagement.Web.Data;

namespace LibraryManagement.Web.Services;

// UC04, UC13: quản lý sách, tìm kiếm sách. (B viết ở đợt 1)
public class BookService
{
    private readonly MongoDbContext _db;

    public BookService(MongoDbContext db) => _db = db;
}
