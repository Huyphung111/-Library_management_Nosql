using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Options;
using LibraryManagement.Web.Services;
using LibraryManagement.Web.ViewModels.Readers;

namespace LibraryManagement.Web.Controllers;

// UC06: Quản lý độc giả (Thủ thư).
[Authorize(Roles = nameof(Role.Librarian))]
public class ReadersController : Controller
{
    private readonly ReaderService _readers;
    private readonly LibraryRules _rules;

    public ReadersController(ReaderService readers, IOptions<LibraryRules> rules)
    {
        _readers = readers;
        _rules = rules.Value;
    }

    // UC06 bước 2
    [HttpGet]
    public async Task<IActionResult> Index(string? keyword, string? status)
    {
        ViewData["Title"] = "Quản lý độc giả";
        var vm = new ReaderIndexVM
        {
            Keyword = keyword,
            Status = status,
            Items = await _readers.GetListAsync(keyword, status)
        };
        return View(vm);
    }

    // UC06: chi tiết, nơi đặt các thao tác khóa, mở khóa, gia hạn thẻ, đặt lại mật khẩu. UC11 cũng mở trang này.
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var vm = await _readers.GetDetailsAsync(id);
        if (vm == null)
        {
            TempData["Error"] = "Không tìm thấy độc giả.";
            return RedirectToAction(nameof(Index));
        }

        // Bước 8: chỉ hiện thông tin tài khoản vừa cấp ngay sau khi thêm
        vm.JustIssued = TempData["IssuedReaderId"] as string == id;
        vm.DefaultPassword = _rules.DefaultPassword;
        ViewData["Title"] = $"Độc giả {vm.Reader.CardNumber}";
        return View(vm);
    }

    // UC06 bước 3–4
    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Thêm độc giả";
        return View(new ReaderFormVM());
    }

    // UC06 bước 5–8
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReaderFormVM model)
    {
        ViewData["Title"] = "Thêm độc giả";
        if (!ModelState.IsValid)
            return View(model);   // bước 6a

        var result = await _readers.CreateAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message!);   // bước 6a: CCCD trùng
            return View(model);
        }

        TempData["Success"] = result.Message;
        TempData["IssuedReaderId"] = result.Data!.Id;
        return RedirectToAction(nameof(Details), new { id = result.Data.Id });
    }

    // UC06 bước 3a
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var reader = await _readers.GetByIdAsync(id);
        if (reader == null)
        {
            TempData["Error"] = "Không tìm thấy độc giả.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = "Sửa thông tin độc giả";
        return View(new ReaderFormVM
        {
            Id = reader.Id,
            CardNumber = reader.CardNumber,
            FullName = reader.FullName,
            DateOfBirth = reader.DateOfBirth.HasValue ? DateHelper.ToVnDate(reader.DateOfBirth.Value) : null,
            IdNumber = reader.IdNumber,
            Phone = reader.Phone ?? string.Empty,
            Email = reader.Email,
            Address = reader.Address
        });
    }

    // UC06 bước 3a
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ReaderFormVM model)
    {
        ViewData["Title"] = "Sửa thông tin độc giả";
        model.Id = id;
        if (!ModelState.IsValid)
            return View(model);

        var result = await _readers.UpdateAsync(id, model);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message!);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    // UC06 bước 3b
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(string id, string? lockReason) =>
        Done(await _readers.LockAsync(id, lockReason), id);

    // UC06 bước 3c
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id) => Done(await _readers.UnlockAsync(id), id);

    // UC06 bước 3d
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtendCard(string id) => Done(await _readers.ExtendCardAsync(id), id);

    // UC06 bước 3e
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id) => Done(await _readers.ResetPasswordAsync(id), id);

    private IActionResult Done(ServiceResult result, string id)
    {
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }
}
