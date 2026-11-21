using Microsoft.Extensions.Options;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Options;

namespace LibraryManagement.Web.Services;

// UC06: quản lý độc giả. (A viết ở đợt 1)
public class ReaderService
{
    private readonly MongoDbContext _db;
    private readonly LibraryRules _rules;
    private readonly CounterService _counters;

    public ReaderService(MongoDbContext db, IOptions<LibraryRules> rules, CounterService counters)
    {
        _db = db;
        _rules = rules.Value;
        _counters = counters;
    }
}
