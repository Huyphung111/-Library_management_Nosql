using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Services;

namespace LibraryManagement.Web.Controllers;

/// <summary>
/// Bảng điều khiển trung tâm dành cho Thủ thư (Member B).
/// </summary>
[Authorize(Roles = nameof(Role.Librarian))]
public class DashboardController : Controller
{
    private readonly ReportService _reportService;

    public DashboardController(ReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = await _reportService.GetDashboardAsync();
        return View(model);
    }
}

