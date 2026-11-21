using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;

namespace LibraryManagement.Web.Services;

/// <summary>Sinh mã tự tăng (DG000001, PM000001) bằng FindOneAndUpdate + $inc, nguyên tử.</summary>
public class CounterService
{
    private readonly MongoDbContext _db;

    public CounterService(MongoDbContext db) => _db = db;

    /// <summary>Mã thẻ độc giả tiếp theo, ví dụ DG000001.</summary>
    public Task<string> NextReaderCodeAsync() => NextCodeAsync("reader", "DG");

    /// <summary>Mã phiếu mượn tiếp theo, ví dụ PM000001.</summary>
    public Task<string> NextLoanCodeAsync() => NextCodeAsync("loan", "PM");

    private async Task<string> NextCodeAsync(string counterName, string prefix)
    {
        var counter = await _db.Counters.FindOneAndUpdateAsync(
            c => c.Id == counterName,
            Builders<Counter>.Update.Inc(c => c.Seq, 1),
            new FindOneAndUpdateOptions<Counter>
            {
                IsUpsert = true,                    // lần đầu tự tạo bộ đếm
                ReturnDocument = ReturnDocument.After
            });

        return $"{prefix}{counter.Seq:D6}";
    }
}
