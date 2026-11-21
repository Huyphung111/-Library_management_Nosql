using LibraryManagement.Web.Data;

namespace LibraryManagement.Web.Services;

// UC05: quản lý thể loại. (B viết ở đợt 1)
public class CategoryService
{
    private readonly MongoDbContext _db;

    public CategoryService(MongoDbContext db) => _db = db;
}
