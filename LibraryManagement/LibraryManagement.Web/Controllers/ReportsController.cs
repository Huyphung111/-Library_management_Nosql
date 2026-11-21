using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Web.Services;

namespace LibraryManagement.Web.Controllers;

/// <summary>
/// UC12: Báo cáo thống kê và biểu đồ trực quan phía Thủ thư.
/// </summary>
[Authorize(Roles = "Librarian")]
public class ReportsController : Controller
{
    private readonly ReportService _reports;

    public ReportsController(ReportService reports)
    {
        _reports = reports;
    }

    /// <summary>
    /// Báo cáo 1: Top sách mượn nhiều nhất.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TopBooks(DateTime? fromDate, DateTime? toDate)
    {
        ViewData["Title"] = "Top sách mượn nhiều";
        var vm = await _reports.GetTopBooksAsync(fromDate, toDate);
        return View(vm);
    }

    /// <summary>
    /// Báo cáo 2: Phiếu mượn quá hạn và tiền phạt tạm tính.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Overdue()
    {
        ViewData["Title"] = "Phiếu mượn quá hạn";
        var vm = await _reports.GetOverdueLoansAsync();
        return View(vm);
    }

    /// <summary>
    /// Báo cáo 3: Lượt mượn theo tháng (Biểu đồ đường Chart.js).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ByMonth(int? year)
    {
        ViewData["Title"] = "Lượt mượn theo tháng";
        var vm = await _reports.GetBorrowByMonthAsync(year);
        return View(vm);
    }

    /// <summary>
    /// Báo cáo 4: Lượt mượn theo thể loại (Biểu đồ tròn Chart.js).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ByCategory(DateTime? fromDate, DateTime? toDate)
    {
        ViewData["Title"] = "Lượt mượn theo thể loại";
        var vm = await _reports.GetBorrowByCategoryAsync(fromDate, toDate);
        return View(vm);
    }

    /// <summary>
    /// Báo cáo 5: Thống kê thu tiền phạt.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Fines(DateTime? fromDate, DateTime? toDate)
    {
        ViewData["Title"] = "Thống kê thu tiền phạt";
        var vm = await _reports.GetFinesAsync(fromDate, toDate);
        return View(vm);
    }
}

