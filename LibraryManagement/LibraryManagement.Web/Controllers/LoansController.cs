using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Options;
using LibraryManagement.Web.Services;
using LibraryManagement.Web.ViewModels.Loans;

namespace LibraryManagement.Web.Controllers;

// UC07 Lập phiếu mượn, UC08 Nhận trả sách (Thủ thư). Details dùng chung cho các trang khác (UC06, UC11).
[Authorize(Roles = nameof(Role.Librarian))]
public class LoansController : Controller
{
    private readonly LoanService _loans;
    private readonly LibraryRules _rules;

    public LoansController(LoanService loans, IOptions<LibraryRules> rules)
    {
        _loans = loans;
        _rules = rules.Value;
    }

    // UC07 bước 1–6. Trang có 2 tab (Danh sách độc giả, Phiếu mượn gần đây); chọn một độc giả thì gọi CheckReader (AJAX)
    // để mở phần chọn sách. ?cardNumber=DG... (ví dụ từ trang Đặt trước) mở thẳng phần chọn sách của độc giả đó.
    [HttpGet]
    public async Task<IActionResult> Create(string? cardNumber)
    {
        var vm = new CreateLoanVM { CardNumber = cardNumber };
        await FillCreateAsync(vm, checkReader: !string.IsNullOrWhiteSpace(cardNumber));
        await FillListsAsync(vm);
        vm.DueDate = vm.BorrowDate.AddDays(_rules.LoanDays);   // bước 6: đề xuất hạn trả (QĐ03)
        return View(vm);
    }

    // UC07 bước 7–8
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateLoanVM model)
    {
        var librarianId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _loans.CreateAsync(model.CardNumber, model.BookIds, model.DueDate, librarianId);
        if (result.Success)
        {
            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
        }

        ModelState.Clear();   // chỉ hiện lỗi nghiệp vụ, tránh trùng với lỗi binding
        ModelState.AddModelError(result.Field ?? string.Empty, result.Message!);
        await FillCreateAsync(model, checkReader: true);
        await FillListsAsync(model);
        // Bước 5a: bỏ các sách đã hết khỏi danh sách đã chọn
        var held = model.Borrower?.HeldBookIds ?? new List<string>();
        var available = model.Books.Where(b => b.AvailableQuantity > 0 || held.Contains(b.Id)).Select(b => b.Id).ToHashSet();
        model.BookIds = model.BookIds.Where(available.Contains).Distinct().ToList();
        return View(model);
    }

    // UC07 bước 2–3 (AJAX): trang Create gọi khi Thủ thư nhập mã thẻ, trả về khối HTML thông tin độc giả
    // và form chọn sách (partial _Borrower), để không phải tải lại cả trang.
    [HttpGet]
    public async Task<IActionResult> CheckReader(string? cardNumber)
    {
        var vm = new CreateLoanVM { CardNumber = cardNumber };
        await FillCreateAsync(vm, checkReader: true);
        vm.DueDate = vm.BorrowDate.AddDays(_rules.LoanDays);
        ViewData["Ajax"] = true;
        return PartialView("_Borrower", vm);
    }

    // Hai tab đầu trang: danh sách độc giả (đặt giữ lên đầu) và phiếu mượn gần đây
    private async Task FillListsAsync(CreateLoanVM vm)
    {
        vm.Readers = await _loans.GetReaderBoardAsync();
        vm.RecentLoans = await _loans.GetRecentLoansAsync();
    }

    private async Task FillCreateAsync(CreateLoanVM vm, bool checkReader)
    {
        ViewData["Title"] = "Lập phiếu mượn";
        vm.BorrowDate = DateHelper.TodayVn();
        vm.LoanDays = _rules.LoanDays;
        if (!checkReader) return;

        var check = await _loans.CheckBorrowerAsync(vm.CardNumber);
        if (!check.Success)
        {
            if (ModelState.GetFieldValidationState(nameof(CreateLoanVM.CardNumber)) != ModelValidationState.Invalid)
                ModelState.AddModelError(nameof(CreateLoanVM.CardNumber), check.Message!);   // bước 2a
            return;
        }

        vm.CardNumber = check.Data!.Reader.CardNumber;
        vm.Borrower = check.Data;
        if (vm.Borrower.CanBorrow)
        {
            vm.Books = await _loans.GetBookOptionsAsync();
            // Độc giả đến nhận sách đã đặt giữ: chọn sẵn các cuốn đang giữ
            if (vm.BookIds.Count == 0)
                vm.BookIds = vm.Borrower.HeldBookIds.ToList();
        }
    }

    // UC08 bước 1–3
    [HttpGet]
    public async Task<IActionResult> Return(string? keyword)
    {
        ViewData["Title"] = "Nhận trả sách";
        return View(await _loans.FindForReturnAsync(keyword));
    }

    // UC08 bước 4–8
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(ReturnLoanInput input, string? keyword)
    {
        var result = await _loans.ReturnAsync(input);
        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Return), new { keyword });
        }

        TempData["Success"] = result.Message;
        TempData["ReturnResult"] = JsonSerializer.Serialize(result.Data);
        return RedirectToAction(nameof(ReturnResult));
    }

    // UC08 bước 7a, 8: kết quả nhận trả và danh sách người đang chờ sách vừa trả
    [HttpGet]
    public IActionResult ReturnResult()
    {
        if (TempData["ReturnResult"] is not string json)
            return RedirectToAction(nameof(Return));

        ViewData["Title"] = "Kết quả nhận trả";
        return View(JsonSerializer.Deserialize<ReturnResultVM>(json));
    }

    // Chi tiết một phiếu mượn (sau khi lập phiếu, từ trang độc giả, từ Tìm kiếm UC11)
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var loan = await _loans.GetByIdAsync(id);
        if (loan == null)
        {
            TempData["Error"] = "Không tìm thấy phiếu mượn.";
            return RedirectToAction(nameof(Create));
        }

        ViewData["Title"] = $"Phiếu mượn {loan.LoanCode}";
        return View(loan);
    }
}
