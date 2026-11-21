using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Web.Services;
using LibraryManagement.Web.ViewModels.Reservations;

namespace LibraryManagement.Web.Controllers;

/// <summary>
/// UC10: Xử lý đặt trước phía Thủ thư.
/// </summary>
[Authorize(Roles = "Librarian")]
public class ReservationsController : Controller
{
    private readonly ReservationService _reservations;

    public ReservationsController(ReservationService reservations)
    {
        _reservations = reservations;
    }

    /// <summary>
    /// Danh sách yêu cầu đặt trước chia theo tab trạng thái.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string tab = "all")
    {
        ViewData["Title"] = "Xử lý đặt trước";
        var vm = await _reservations.GetLibrarianListAsync(tab);
        return View(vm);
    }

    /// <summary>
    /// Thủ thư duyệt yêu cầu đặt trước (UC10 bước 5).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string id, string returnTab = "pending")
    {
        var librarianId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await _reservations.ApproveAsync(id, librarianId);

        if (result.Success)
            TempData["Success"] = result.Message;
        else
            TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Index), new { tab = returnTab });
    }

    /// <summary>
    /// Thủ thư từ chối yêu cầu đặt trước kèm lý do (UC10 bước 5a).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(ReservationActionVM model, string returnTab = "pending")
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng nhập lý do từ chối yêu cầu đặt trước.";
            return RedirectToAction(nameof(Index), new { tab = returnTab });
        }

        var librarianId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await _reservations.RejectAsync(model.Id, librarianId, model.Reason);

        if (result.Success)
            TempData["Success"] = result.Message;
        else
            TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Index), new { tab = returnTab });
    }

    /// <summary>
    /// Thủ thư hủy yêu cầu đặt trước đã duyệt (UC10 bước 3b).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(ReservationActionVM model, string returnTab = "approved")
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng nhập lý do hủy yêu cầu đặt trước.";
            return RedirectToAction(nameof(Index), new { tab = returnTab });
        }

        var librarianId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await _reservations.CancelByLibrarianAsync(model.Id, librarianId, model.Reason);

        if (result.Success)
            TempData["Success"] = result.Message;
        else
            TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Index), new { tab = returnTab });
    }
}
