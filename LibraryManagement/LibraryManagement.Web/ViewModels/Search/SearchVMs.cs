using Microsoft.AspNetCore.Mvc.Rendering;
using LibraryManagement.Web.Models;

namespace LibraryManagement.Web.ViewModels.Search;

/// <summary>
/// Bộ lọc tra cứu đa năng của Thủ thư (UC11).
/// </summary>
public class SearchFilterVM
{
    /// <summary>Loại đối tượng tra cứu: "book", "reader", "loan".</summary>
    public string Type { get; set; } = "book";

    /// <summary>Từ khóa tìm kiếm chung.</summary>
    public string? Keyword { get; set; }

    /// <summary>Bộ lọc thể loại (khi Type = "book").</summary>
    public string? CategoryId { get; set; }

    /// <summary>Bộ lọc trạng thái phiếu (khi Type = "loan"): "all", "borrowing", "returned", "overdue".</summary>
    public string? LoanStatus { get; set; }

    /// <summary>Khoảng ngày mượn - từ ngày (khi Type = "loan").</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Khoảng ngày mượn - đến ngày (khi Type = "loan").</summary>
    public DateTime? ToDate { get; set; }
}

/// <summary>Kết quả tra cứu Sách.</summary>
public class BookSearchResultVM
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public List<string> CategoryNames { get; set; } = new();
    public string Publisher { get; set; } = string.Empty;
    public int? PublishYear { get; set; }
    public long Price { get; set; }
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
}

/// <summary>Kết quả tra cứu Độc giả.</summary>
public class ReaderSearchResultVM
{
    public string Id { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime IssuedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public CardStatus CardStatus { get; set; }
    public bool IsExpired { get; set; }
    public string? LockReason { get; set; }
}

/// <summary>Kết quả tra cứu Phiếu mượn.</summary>
public class LoanSearchResultVM
{
    public string Id { get; set; } = string.Empty;
    public string LoanCode { get; set; } = string.Empty;
    public string ReaderId { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string ReaderName { get; set; } = string.Empty;
    public DateTime BorrowDate { get; set; }
    public DateTime DueDate { get; set; }
    public LoanStatus Status { get; set; }
    public bool IsOverdue { get; set; }
    public int OverdueDays { get; set; }
    public long TotalFine { get; set; }
    public List<string> BookTitles { get; set; } = new();
    public int ItemsCount { get; set; }
    public int ReturnedCount { get; set; }
}

/// <summary>
/// ViewModel toàn diện cho màn hình Tra cứu (UC11).
/// </summary>
public class SearchIndexVM
{
    public SearchFilterVM Filter { get; set; } = new();
    public List<BookSearchResultVM> Books { get; set; } = new();
    public List<ReaderSearchResultVM> Readers { get; set; } = new();
    public List<LoanSearchResultVM> Loans { get; set; } = new();
    public List<SelectListItem> Categories { get; set; } = new();
    public bool HasSearched { get; set; }

    public int TotalResults => Filter.Type.ToLower() switch
    {
        "reader" => Readers.Count,
        "loan" => Loans.Count,
        _ => Books.Count
    };
}

