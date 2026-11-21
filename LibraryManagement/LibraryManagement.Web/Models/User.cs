using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LibraryManagement.Web.Models;

/// <summary>Collection "users": tài khoản đăng nhập.</summary>
public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; }

    /// <summary>Hồ sơ độc giả tương ứng. Null nếu là Thủ thư.</summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ReaderId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
