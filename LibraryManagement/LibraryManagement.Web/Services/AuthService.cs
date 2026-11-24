using Microsoft.Extensions.Options;
using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Options;

namespace LibraryManagement.Web.Services;

/// <summary>Thông tin trả về khi đăng nhập thành công, để controller tạo cookie.</summary>
public class LoginResult
{
    public required User User { get; init; }

    /// <summary>
    /// Câu báo thẻ bị khóa hoặc hết hạn (UC01 bước 5a), lưu vào claim "CardNotice" (mục 6.3 thiết kế kỹ thuật).
    /// Null nếu là Thủ thư hoặc thẻ đang dùng được.
    /// </summary>
    public string? CardNotice { get; init; }
}

// UC01, UC03: đăng nhập, đổi mật khẩu.
public class AuthService
{
    private readonly MongoDbContext _db;
    private readonly LibraryRules _rules;

    public AuthService(MongoDbContext db, IOptions<LibraryRules> rules)
    {
        _db = db;
        _rules = rules.Value;
    }

    // UC01: kiểm tra tên đăng nhập, mật khẩu; với độc giả thì kiểm tra thêm tình trạng thẻ.
    public async Task<ServiceResult<LoginResult>> LoginAsync(string username, string password)
    {
        const string wrongLogin = "Tên đăng nhập hoặc mật khẩu không đúng.";   // bước 4a: không nói rõ sai phần nào

        username = username.Trim();
        // Bước 4: tên đăng nhập của độc giả là mã thẻ (luôn lưu dạng DG000001), cho phép gõ chữ thường
        if (username.StartsWith("DG", StringComparison.OrdinalIgnoreCase))
            username = username.ToUpperInvariant();
        var user = await _db.Users.Find(u => u.Username == username).FirstOrDefaultAsync();
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return ServiceResult<LoginResult>.Fail(wrongLogin);

        string? cardNotice = null;
        if (user.Role == Role.Reader)
        {
            var reader = await _db.Readers.Find(r => r.Id == user.ReaderId).FirstOrDefaultAsync();
            if (reader == null)
                return ServiceResult<LoginResult>.Fail("Không tìm thấy hồ sơ độc giả của tài khoản này. Vui lòng liên hệ thủ thư.");

            cardNotice = BuildCardNotice(reader);   // bước 5a: vẫn cho đăng nhập, chỉ báo tình trạng thẻ
        }

        return ServiceResult<LoginResult>.Ok(new LoginResult { User = user, CardNotice = cardNotice });
    }

    // UC03: đổi mật khẩu của chính người đang đăng nhập.
    public async Task<ServiceResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
            return ServiceResult.Fail("Không tìm thấy tài khoản. Vui lòng đăng nhập lại.");

        // Bước 4a
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return ServiceResult.Fail("Mật khẩu hiện tại không đúng.", "CurrentPassword");

        // Bước 4b (QĐ10)
        if (newPassword.Length < _rules.MinPasswordLength)
            return ServiceResult.Fail($"Mật khẩu mới phải có ít nhất {_rules.MinPasswordLength} ký tự.", "NewPassword");
        if (newPassword == currentPassword)
            return ServiceResult.Fail("Mật khẩu mới phải khác mật khẩu hiện tại.", "NewPassword");

        // Bước 5
        await _db.Users.UpdateOneAsync(
            u => u.Id == userId,
            Builders<User>.Update.Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(newPassword)));

        return ServiceResult.Ok("Đổi mật khẩu thành công.");
    }

    /// <summary>
    /// Câu cảnh báo thẻ hiện tại của độc giả (null nếu thẻ dùng được). Cookie gọi lại trong phiên
    /// để thanh CardNotice đổi theo khi Thủ thư khóa / mở khóa / gia hạn thẻ (BUG-10, mục 6.3 thiết kế kỹ thuật).
    /// </summary>
    public async Task<string?> GetCardNoticeAsync(string readerId)
    {
        var reader = await _db.Readers.Find(r => r.Id == readerId).FirstOrDefaultAsync();
        return reader == null ? null : BuildCardNotice(reader);
    }

    private static string? BuildCardNotice(Reader reader)
    {
        const string limit = "Bạn chỉ xem được thông tin, chưa mượn, gia hạn hay đặt trước sách được.";

        if (reader.CardStatus == CardStatus.Locked)
        {
            var reason = string.IsNullOrWhiteSpace(reader.LockReason) ? "" : $" (lý do: {reader.LockReason})";
            return $"Thẻ của bạn đang bị khóa{reason}. {limit} Vui lòng liên hệ thủ thư.";
        }

        if (reader.IsExpired)
            return $"Thẻ của bạn đã hết hạn ngày {DateHelper.ToVnDate(reader.ExpiryDate):dd/MM/yyyy}. {limit} Vui lòng liên hệ thủ thư để gia hạn thẻ.";

        return null;
    }
}
