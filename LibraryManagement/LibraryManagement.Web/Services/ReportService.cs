using LibraryManagement.Web.Data;

namespace LibraryManagement.Web.Services;

// UC12: thống kê bằng aggregation pipeline. (B viết ở đợt 3)
public class ReportService
{
    private readonly MongoDbContext _db;

    public ReportService(MongoDbContext db) => _db = db;
}
