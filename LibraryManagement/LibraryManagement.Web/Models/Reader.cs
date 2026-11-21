using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using LibraryManagement.Web.Services;

namespace LibraryManagement.Web.Models;

/// <summary>Collection "readers": hồ sơ độc giả và thẻ thư viện.</summary>
public class Reader
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>Mã thẻ, dạng DG000001.</summary>
    public string CardNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }

    /// <summary>Số CCCD, không được trùng.</summary>
    public string IdNumber { get; set; } = string.Empty;

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    public DateTime IssuedDate { get; set; }
    public DateTime ExpiryDate { get; set; }

    public CardStatus CardStatus { get; set; } = CardStatus.Active;
    public string? LockReason { get; set; }

    /// <summary>Thẻ hết hạn. Tính khi dùng, không lưu trong database.</summary>
    [BsonIgnore]
    public bool IsExpired => DateHelper.ToVnDate(ExpiryDate) < DateHelper.TodayVn();
}
