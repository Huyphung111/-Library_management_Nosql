using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LibraryManagement.Web.Models;

/// <summary>Collection "books": đầu sách. Tác giả nhúng dạng mảng.</summary>
public class Book
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public List<string> Authors { get; set; } = new();
    public string? Publisher { get; set; }
    public int? PublishYear { get; set; }

    /// <summary>Giá sách (đồng). Dùng để tính tiền phạt khi mất sách.</summary>
    public long Price { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> CategoryIds { get; set; } = new();

    public int TotalQuantity { get; set; }

    /// <summary>Số cuốn còn trên kệ. Chỉ trừ bằng update có điều kiện "availableQuantity &gt; 0".</summary>
    public int AvailableQuantity { get; set; }

    /// <summary>Ảnh bìa trong GridFS (bucket "covers", collection covers.files). Null nếu chưa có ảnh. Mục 7.5.</summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string? CoverImageId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
