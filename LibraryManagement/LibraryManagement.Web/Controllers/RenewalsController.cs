using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Services;
using LibraryManagement.Web.ViewModels.Loans;

namespace LibraryManagement.Web.Controllers;

// UC09 Gia hạn phiếu mượn (Thủ thư): duyệt / từ chối yêu cầu online, gia hạn tại quầy.
[Authorize(Roles = nameof(Role.Librarian))]
public class RenewalsController : Controller
{
    private readonly LoanService _loans;

    public RenewalsController(LoanService loans) => _loans = loans;

    private string LibrarianId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // UC09 bước 2 (danh sách Chờ duyệt) và bước 1a (nhập mã phiếu ?loanCode= để gia hạn tại quầy)
    [HttpGet]
    public async Task<IActionResult> Index(string? loanCode)
    {
        ViewData["Title"] = "Gia hạn phiếu mượn";
        var vm = new RenewalIndexVM
        {
            Pending = await _loans.GetPendingRenewalsAsync(),
            LoanCode = loanCode
        };

        if (!string.IsNullOrWhiteSpace(loanCode))
        {
            var counter = await _loans.GetCounterRenewalAsync(loanCode);
            if (counter.Success) vm.Counter = counter.Data;
            else vm.CounterNotice = counter.Message;
        }
        return View(vm);
    }

    // UC09 bước 5–6
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string loanId) =>
        Done(await _loans.ApproveAsync(loanId, LibrarianId));

    // UC09 bước 5a
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string loanId, string? reason) =>
        Done(await _loans.RejectAsync(loanId, LibrarianId, reason));

    // UC09 bước 1a
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenewAtCounter(string loanId, string? loanCode) =>
        Done(await _loans.RenewAtCounterAsync(loanId, LibrarianId), loanCode);

    private IActionResult Done(ServiceResult result, string? loanCode = null)
    {
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index), new { loanCode = result.Success ? null : loanCode });
    }
}
