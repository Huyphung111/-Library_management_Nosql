using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using LibraryManagement.Web.Services;

namespace LibraryManagement.Web.Models;

/// <summary>Collection "loans": phiếu mượn. Sách, tiền phạt và yêu cầu gia hạn nhúng bên trong.</summary>
public class Loan
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>Mã phiếu, dạng PM000001.</summary>
    public string LoanCode { get; set; } = string.Empty;

    /// <summary>Bản sao thông tin độc giả để hiển thị, không cần $lookup.</summary>
    public LoanReader Reader { get; set; } = new();

    /// <summary>Thủ thư lập phiếu.</summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string? LibrarianId { get; set; }

    public DateTime BorrowDate { get; set; }
    public DateTime DueDate { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Borrowing;
    public int RenewalCount { get; set; }

    public List<LoanItem> Items { get; set; } = new();

    /// <summary>Tổng tiền phạt của cả phiếu (đồng).</summary>
    public long TotalFine { get; set; }

    public List<RenewalRequest> RenewalRequests { get; set; } = new();

    /// <summary>Phiếu quá hạn: đang mượn và hạn trả trước hôm nay. Tính khi dùng, không lưu.</summary>
    [BsonIgnore]
    public bool IsOverdue => Status == LoanStatus.Borrowing && DateHelper.DaysOverdue(DueDate) > 0;

    /// <summary>Số ngày quá hạn tính đến hôm nay (0 nếu chưa quá hạn).</summary>
    [BsonIgnore]
    public int OverdueDays => DateHelper.DaysOverdue(DueDate);
}

/// <summary>Bản sao thông tin độc giả nhúng trong phiếu mượn.</summary>
public class LoanReader
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string ReaderId { get; set; } = string.Empty;

    public string CardNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

/// <summary>Một cuốn sách trong phiếu mượn.</summary>
public class LoanItem
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string BookId { get; set; } = string.Empty;

    /// <summary>Bản sao tên sách.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Bản sao thể loại, dùng cho thống kê theo thể loại.</summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> CategoryIds { get; set; } = new();

    /// <summary>Null nếu chưa trả.</summary>
    public DateTime? ReturnDate { get; set; }

    public BookCondition? Condition { get; set; }

    /// <summary>Tiền phạt của cuốn này (đồng).</summary>
    public long Fine { get; set; }
}

/// <summary>Yêu cầu gia hạn nhúng trong phiếu mượn.</summary>
public class RenewalRequest
{
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public RenewalSource Source { get; set; } = RenewalSource.Online;
    public RenewalStatus Status { get; set; } = RenewalStatus.Pending;
    public DateTime? ProcessedAt { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ProcessedBy { get; set; }

    public string? RejectReason { get; set; }
}
