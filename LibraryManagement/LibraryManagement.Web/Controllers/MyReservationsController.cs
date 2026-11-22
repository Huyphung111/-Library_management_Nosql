using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Web.Services;

namespace LibraryManagement.Web.Controllers;

/// <summary>
/// UC16: Đặt trước sách phía Độc giả.
/// </summary>
[Authorize(Roles = "Reader")]
public class MyReservationsController : Controller
{
    private readonly ReservationService _reservations;

    public MyReservationsController(ReservationService reservations)
    {
        _reservations = reservations;
    }

    /// <summary>
    /// Trang "Đặt trước của tôi": xem danh sách các sách mình đã đăng ký đặt trước.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Đặt trước của tôi";
        var readerId = User.FindFirstValue("ReaderId");
        if (string.IsNullOrEmpty(readerId))
            return Forbid();

        var list = await _reservations.GetMyReservationsAsync(readerId);
        return View(list);
    }

    /// <summary>
    /// Màn hình xác nhận đặt trước sách (UC16 bước 3).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(string bookId)
    {
        if (string.IsNullOrEmpty(bookId))
            return NotFound();

        ViewData["Title"] = "Xác nhận đặt trước sách";
        var vm = await _reservations.GetBookForReservationConfirmAsync(bookId);
        if (vm == null)
            return NotFound();

        return View(vm);
    }

    /// <summary>
    /// Thực hiện gửi yêu cầu đặt trước sách (UC16 bước 4, 5).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string bookId, bool confirmed)
    {
        var readerId = User.FindFirstValue("ReaderId");
        if (string.IsNullOrEmpty(readerId))
            return Forbid();

        var result = await _reservations.CreateAsync(readerId, bookId);
        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Create), new { bookId });
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Độc giả tự hủy yêu cầu đặt trước (UC16 bước 1b).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string id)
    {
        var readerId = User.FindFirstValue("ReaderId");
        if (string.IsNullOrEmpty(readerId))
            return Forbid();

        var result = await _reservations.CancelByReaderAsync(readerId, id);
        if (!result.Success)
            TempData["Error"] = result.Message;
        else
            TempData["Success"] = result.Message;

        return RedirectToAction(nameof(Index));
    }
}

