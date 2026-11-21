using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LibraryManagement.Web.Models;

/// <summary>Collection "reservations": yêu cầu đặt trước (không giữ sách riêng).</summary>
public class Reservation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string ReaderId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string BookId { get; set; } = string.Empty;

    /// <summary>Bản sao tên sách.</summary>
    public string BookTitle { get; set; } = string.Empty;

    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ProcessedBy { get; set; }

    /// <summary>Lý do từ chối hoặc hủy.</summary>
    public string? Reason { get; set; }

    /// <summary>Phiếu mượn đã hoàn tất yêu cầu này.</summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string? CompletedLoanId { get; set; }
}
