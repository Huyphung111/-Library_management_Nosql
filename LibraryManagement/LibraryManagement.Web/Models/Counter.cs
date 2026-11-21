using MongoDB.Bson.Serialization.Attributes;

namespace LibraryManagement.Web.Models;

/// <summary>Collection "counters": bộ đếm kỹ thuật để sinh mã thẻ, mã phiếu tự tăng.</summary>
public class Counter
{
    /// <summary>Tên bộ đếm, ví dụ "reader", "loan". Lưu dạng chuỗi, không phải ObjectId.</summary>
    [BsonId]
    public string Id { get; set; } = string.Empty;

    public long Seq { get; set; }
}
