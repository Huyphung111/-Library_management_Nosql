using LibraryManagement.Web.Models;

namespace LibraryManagement.Web.ViewModels.Reports;

/// <summary>
/// Báo cáo 1: Top sách mượn nhiều nhất (UC12).
/// </summary>
public class TopBooksReportVM
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? ErrorMessage { get; set; }
    public List<TopBookItemVM> Items { get; set; } = new();
}

public class TopBookItemVM
{
    public string BookId { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public List<string> CategoryNames { get; set; } = new();
    public int BorrowCount { get; set; }
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
}

/// <summary>
/// Báo cáo 2: Danh sách phiếu mượn quá hạn (UC12).
/// </summary>
public class OverdueReportVM
{
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
    public int TotalOverdueLoans => Items.Count;
    public long TotalEstimatedFine => Items.Sum(i => i.EstimatedFine);
    public List<OverdueLoanItemVM> Items { get; set; } = new();
}

public class OverdueLoanItemVM
{
    public string LoanId { get; set; } = string.Empty;
    public string LoanCode { get; set; } = string.Empty;
    public string ReaderId { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string ReaderName { get; set; } = string.Empty;
    public string? ReaderPhone { get; set; }
    public DateTime BorrowDate { get; set; }
    public DateTime DueDate { get; set; }
    public int OverdueDays { get; set; }
    public int OverdueBooksCount { get; set; }
    public List<string> BookTitles { get; set; } = new();
    public long EstimatedFine { get; set; }
}

/// <summary>
/// Báo cáo 3: Lượt mượn theo tháng trong năm (UC12) - Dùng Chart.js vẽ biểu đồ đường.
/// </summary>
public class BorrowByMonthReportVM
{
    public int SelectedYear { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public List<MonthStatVM> Months { get; set; } = new();

    public int TotalLoansInYear => Months.Sum(m => m.LoanCount);
    public int TotalBooksInYear => Months.Sum(m => m.BookCount);
}

public class MonthStatVM
{
    public int Month { get; set; }
    public string MonthName => $"Tháng {Month}";
    public int LoanCount { get; set; }
    public int BookCount { get; set; }
}

/// <summary>
/// Báo cáo 4: Lượt mượn theo thể loại (UC12) - Dùng Chart.js vẽ biểu đồ tròn.
/// </summary>
public class BorrowByCategoryReportVM
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalBorrowCount => Categories.Sum(c => c.BorrowCount);
    public List<CategoryStatVM> Categories { get; set; } = new();
}

public class CategoryStatVM
{
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int BorrowCount { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Báo cáo 5: Thống kê thu tiền phạt (UC12).
/// </summary>
public class FinesReportVM
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? ErrorMessage { get; set; }
    public long TotalFines => Items.Sum(i => i.FineAmount);
    public List<FineDetailVM> Items { get; set; } = new();
}

public class FineDetailVM
{
    public string LoanCode { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string ReaderName { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public BookCondition? Condition { get; set; }
    public long FineAmount { get; set; }
    public string FineType { get; set; } = string.Empty;
}
