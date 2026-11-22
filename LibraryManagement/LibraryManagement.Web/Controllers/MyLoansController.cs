using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Services;

namespace LibraryManagement.Web.Controllers;

// UC14 Xem sách đang mượn và lịch sử, UC15 Yêu cầu gia hạn (Độc giả).
// ReaderId luôn lấy từ claim, không nhận từ URL hay form, để không xem / sửa được phiếu của người khác.
[Authorize(Roles = nameof(Role.Reader))]
public class MyLoansController : Controller
{
    private readonly LoanService _loans;

    public MyLoansController(LoanService loans) => _loans = loans;

    private string ReaderId => User.FindFirst("ReaderId")!.Value;

    // UC14 bước 1–2: tab "Đang mượn"
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Sách đang mượn";
        return View(await _loans.GetCurrentLoansAsync(ReaderId));
    }

    // UC14 bước 3–4: tab "Lịch sử"
    [HttpGet]
    public async Task<IActionResult> History()
    {
        ViewData["Title"] = "Lịch sử mượn";
        return View(await _loans.GetHistoryAsync(ReaderId));
    }

    // UC15 bước 1–3: kiểm tra điều kiện, hiện hạn trả hiện tại và hạn dự kiến
    [HttpGet]
    public async Task<IActionResult> RequestRenewal(string id)
    {
        var result = await _loans.GetRenewalPreviewAsync(ReaderId, id);
        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = "Yêu cầu gia hạn";
        return View(result.Data);
    }

    // UC15 bước 4–5. Bước 4a (Hủy) chỉ là link quay lại Index.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(RequestRenewal))]
    public async Task<IActionResult> RequestRenewalPost(string id)
    {
        var result = await _loans.RequestRenewalAsync(ReaderId, id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
