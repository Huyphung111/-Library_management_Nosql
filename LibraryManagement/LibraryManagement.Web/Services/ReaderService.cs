using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Options;
using LibraryManagement.Web.ViewModels.Readers;

namespace LibraryManagement.Web.Services;

// UC06: quản lý độc giả.
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

    // UC06 bước 2: danh sách, tìm theo mã thẻ / họ tên / SĐT, lọc theo trạng thái thẻ.
    // "Hết hạn" tính khi truy vấn (expiryDate < 00:00 hôm nay giờ VN), không lưu trong database.
    public async Task<List<ReaderListVM>> GetListAsync(string? keyword, string? status)
    {
        var f = Builders<Reader>.Filter;
        var filter = f.Empty;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var regex = new BsonRegularExpression(Regex.Escape(keyword.Trim()), "i");
            filter &= f.Or(f.Regex(r => r.CardNumber, regex), f.Regex(r => r.FullName, regex), f.Regex(r => r.Phone, regex));
        }

        var today = DateHelper.StartOfTodayVnUtc();
        filter &= status switch
        {
            "active" => f.Eq(r => r.CardStatus, CardStatus.Active) & f.Gte(r => r.ExpiryDate, today),
            "locked" => f.Eq(r => r.CardStatus, CardStatus.Locked),
            "expired" => f.Lt(r => r.ExpiryDate, today),
            _ => f.Empty
        };

        var readers = await _db.Readers.Find(filter).SortBy(r => r.CardNumber).ToListAsync();
        return readers.Select(r => new ReaderListVM
        {
            Id = r.Id!,
            CardNumber = r.CardNumber,
            FullName = r.FullName,
            Phone = r.Phone,
            ExpiryDate = DateHelper.ToVnDate(r.ExpiryDate),
            CardStatus = r.CardStatus,
            IsExpired = r.IsExpired
        }).ToList();
    }

    public async Task<Reader?> GetByIdAsync(string? id)
    {
        if (!ObjectId.TryParse(id, out _)) return null;   // id sai định dạng trên URL thì coi như không có
        return await _db.Readers.Find(r => r.Id == id).FirstOrDefaultAsync();
    }

    // UC06: chi tiết độc giả kèm các phiếu mượn. Dùng cả cho UC11 khi Thủ thư chọn một kết quả.
    public async Task<ReaderDetailsVM?> GetDetailsAsync(string id)
    {
        var reader = await GetByIdAsync(id);
        if (reader == null) return null;

        var user = await _db.Users.Find(u => u.ReaderId == id).FirstOrDefaultAsync();
        var loans = await _db.Loans.Find(l => l.Reader.ReaderId == id)
            .SortByDescending(l => l.BorrowDate).ToListAsync();

        return new ReaderDetailsVM
        {
            Reader = reader,
            Username = user?.Username,
            Loans = loans,
            BorrowingBooks = loans.Where(l => l.Status == LoanStatus.Borrowing)
                .Sum(l => l.Items.Count(i => i.ReturnDate == null))
        };
    }

    // UC06 bước 5–8: thêm độc giả và cấp tài khoản (tên đăng nhập = mã thẻ, mật khẩu mặc định).
    // Hồ sơ readers và tài khoản users tạo trong cùng transaction để không bị tạo một nửa.
    public async Task<ServiceResult<Reader>> CreateAsync(ReaderFormVM vm)
    {
        var check = await ValidateAsync(vm, null);
        if (check != null) return ServiceResult<Reader>.Fail(check.Message!, check.Field);

        // Lấy mã thẻ sau khi đã kiểm tra xong, để CCCD trùng không làm bộ đếm nhảy số vô ích
        var cardNumber = await _counters.NextReaderCodeAsync();
        var today = DateHelper.TodayVn();
        var reader = new Reader
        {
            CardNumber = cardNumber,
            FullName = vm.FullName.Trim(),
            DateOfBirth = vm.DateOfBirth.HasValue ? DateHelper.VnDateToUtc(vm.DateOfBirth.Value) : null,
            IdNumber = vm.IdNumber.Trim(),
            Phone = vm.Phone.Trim(),
            Email = Clean(vm.Email),
            Address = Clean(vm.Address),
            IssuedDate = DateHelper.VnDateToUtc(today),
            ExpiryDate = DateHelper.VnDateToUtc(today.AddYears(_rules.CardValidYears)),   // QĐ01
            CardStatus = CardStatus.Active
        };

        try
        {
            using var session = await _db.Client.StartSessionAsync();
            await session.WithTransactionAsync(async (s, ct) =>
            {
                await _db.Readers.InsertOneAsync(s, reader, cancellationToken: ct);
                await _db.Users.InsertOneAsync(s, new User
                {
                    Username = cardNumber,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(_rules.DefaultPassword),
                    Role = Role.Reader,
                    ReaderId = reader.Id,
                    FullName = reader.FullName,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken: ct);
                return true;
            });
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            // Hai thủ thư cùng nhập một CCCD đúng lúc: index unique chặn lại
            reader.Id = null;
            return ServiceResult<Reader>.Fail("Số CCCD này đã được dùng cho độc giả khác.", nameof(ReaderFormVM.IdNumber));
        }

        return ServiceResult<Reader>.Ok(reader, $"Đã thêm độc giả {reader.FullName}, mã thẻ {cardNumber}.");
    }

    // UC06 bước 3a: sửa thông tin. Mã thẻ, ngày cấp, hạn thẻ, trạng thái không đổi ở đây.
    // Họ tên có bản sao trong users và loans (reader.fullName) nên cập nhật luôn các bản sao.
    public async Task<ServiceResult> UpdateAsync(string id, ReaderFormVM vm)
    {
        var reader = await GetByIdAsync(id);
        if (reader == null) return ServiceResult.Fail("Không tìm thấy độc giả.");

        var check = await ValidateAsync(vm, id);
        if (check != null) return check;

        var fullName = vm.FullName.Trim();
        var update = Builders<Reader>.Update
            .Set(r => r.FullName, fullName)
            .Set(r => r.DateOfBirth, vm.DateOfBirth.HasValue ? DateHelper.VnDateToUtc(vm.DateOfBirth.Value) : null)
            .Set(r => r.IdNumber, vm.IdNumber.Trim())
            .Set(r => r.Phone, vm.Phone.Trim())
            .Set(r => r.Email, Clean(vm.Email))
            .Set(r => r.Address, Clean(vm.Address));

        try
        {
            using var session = await _db.Client.StartSessionAsync();
            await session.WithTransactionAsync(async (s, ct) =>
            {
                await _db.Readers.UpdateOneAsync(s, r => r.Id == id, update, cancellationToken: ct);
                if (fullName != reader.FullName)
                {
                    await _db.Users.UpdateManyAsync(s, u => u.ReaderId == id,
                        Builders<User>.Update.Set(u => u.FullName, fullName), cancellationToken: ct);
                    await _db.Loans.UpdateManyAsync(s, l => l.Reader.ReaderId == id,
                        Builders<Loan>.Update.Set(l => l.Reader.FullName, fullName), cancellationToken: ct);
                }
                return true;
            });
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            return ServiceResult.Fail("Số CCCD này đã được dùng cho độc giả khác.", nameof(ReaderFormVM.IdNumber));
        }

        return ServiceResult.Ok("Cập nhật thông tin độc giả thành công.");
    }

    // UC06 bước 3b: khóa thẻ kèm lý do.
    public async Task<ServiceResult> LockAsync(string id, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return ServiceResult.Fail("Vui lòng nhập lý do khóa thẻ.", "LockReason");
        if (!ValidId(id)) return ServiceResult.Fail("Không tìm thấy độc giả.");

        var result = await _db.Readers.UpdateOneAsync(
            r => r.Id == id && r.CardStatus == CardStatus.Active,
            Builders<Reader>.Update.Set(r => r.CardStatus, CardStatus.Locked).Set(r => r.LockReason, reason.Trim()));

        return result.ModifiedCount == 1
            ? ServiceResult.Ok("Đã khóa thẻ độc giả.")
            : ServiceResult.Fail("Không khóa được: không tìm thấy độc giả hoặc thẻ đã bị khóa.");
    }

    // UC06 bước 3c: mở khóa thẻ.
    public async Task<ServiceResult> UnlockAsync(string id)
    {
        if (!ValidId(id)) return ServiceResult.Fail("Không tìm thấy độc giả.");
        var result = await _db.Readers.UpdateOneAsync(
            r => r.Id == id && r.CardStatus == CardStatus.Locked,
            Builders<Reader>.Update.Set(r => r.CardStatus, CardStatus.Active).Set(r => r.LockReason, null));

        return result.ModifiedCount == 1
            ? ServiceResult.Ok("Đã mở khóa thẻ độc giả.")
            : ServiceResult.Fail("Không mở khóa được: không tìm thấy độc giả hoặc thẻ đang hoạt động.");
    }

    // UC06 bước 3d: gia hạn thẻ, cộng CardValidYears năm vào ngày hết hạn hiện tại (QĐ01).
    public async Task<ServiceResult> ExtendCardAsync(string id)
    {
        var reader = await GetByIdAsync(id);
        if (reader == null) return ServiceResult.Fail("Không tìm thấy độc giả.");

        var newExpiry = DateHelper.ToVnDate(reader.ExpiryDate).AddYears(_rules.CardValidYears);
        // Lọc theo hạn cũ để hai lần bấm cùng lúc không cộng hai lần
        var result = await _db.Readers.UpdateOneAsync(
            r => r.Id == id && r.ExpiryDate == reader.ExpiryDate,
            Builders<Reader>.Update.Set(r => r.ExpiryDate, DateHelper.VnDateToUtc(newExpiry)));

        if (result.ModifiedCount == 0)
            return ServiceResult.Fail("Thẻ vừa được cập nhật bởi người khác, vui lòng thử lại.");

        var note = newExpiry < DateHelper.TodayVn() ? " Thẻ vẫn còn hết hạn, có thể gia hạn thêm." : "";
        return ServiceResult.Ok($"Đã gia hạn thẻ đến ngày {newExpiry:dd/MM/yyyy}.{note}");
    }

    // UC06 bước 3e: đặt lại mật khẩu của độc giả về mặc định.
    public async Task<ServiceResult> ResetPasswordAsync(string id)
    {
        if (!ValidId(id)) return ServiceResult.Fail("Không tìm thấy độc giả.");
        var result = await _db.Users.UpdateOneAsync(
            u => u.ReaderId == id && u.Role == Role.Reader,
            Builders<User>.Update.Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(_rules.DefaultPassword)));

        return result.MatchedCount == 1
            ? ServiceResult.Ok("Đã đặt lại mật khẩu về mặc định.")
            : ServiceResult.Fail("Không tìm thấy tài khoản của độc giả này.");
    }

    // UC06 bước 6: kiểm tra nghiệp vụ (DataAnnotations đã kiểm tra trống, định dạng). Null nghĩa là hợp lệ.
    private async Task<ServiceResult?> ValidateAsync(ReaderFormVM vm, string? exceptId)
    {
        if (vm.DateOfBirth.HasValue && vm.DateOfBirth.Value.Date >= DateHelper.TodayVn())
            return ServiceResult.Fail("Ngày sinh phải trước ngày hôm nay.", nameof(ReaderFormVM.DateOfBirth));

        var idNumber = vm.IdNumber.Trim();
        var duplicated = await _db.Readers.Find(r => r.IdNumber == idNumber && r.Id != exceptId).AnyAsync();
        if (duplicated)
            return ServiceResult.Fail("Số CCCD này đã được dùng cho độc giả khác.", nameof(ReaderFormVM.IdNumber));

        return null;
    }

    private static bool ValidId(string? id) => ObjectId.TryParse(id, out _);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool IsDuplicateKey(Exception ex) => ex switch
    {
        MongoWriteException w => w.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoCommandException c => c.Code == 11000,
        _ => false
    };
}
