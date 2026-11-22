using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Services;
using LibraryManagement.Web.ViewModels.Books;

namespace LibraryManagement.Web.Controllers;

/// <summary>
/// UC04: Quản lý sách dành cho Thủ thư (Member B).
/// </summary>
[Authorize(Roles = nameof(Role.Librarian))]
public class BooksController : Controller
{
    private readonly BookService _bookService;
    private readonly CategoryService _categoryService;

    public BooksController(BookService bookService, CategoryService categoryService)
    {
        _bookService = bookService;
        _categoryService = categoryService;
    }

    // GET: /Books
    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? categoryId)
    {
        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;

        var categories = await _categoryService.GetAllWithBookCountAsync();
        ViewBag.Categories = categories.Select(c => new SelectListItem
        {
            Value = c.Id,
            Text = c.Name,
            Selected = c.Id == categoryId
        }).ToList();

        var list = await _bookService.GetAllAsync(search, categoryId);
        return View(list);
    }

    // GET: /Books/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new BookFormVM();
        await PopulateCategoryOptions(vm);
        return View(vm);
    }

    // POST: /Books/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookFormVM model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoryOptions(model);
            return View(model);
        }

        var result = await _bookService.CreateAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message!);
            await PopulateCategoryOptions(model);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // GET: /Books/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var book = await _bookService.GetByIdAsync(id);
        if (book == null)
        {
            TempData["Error"] = "Không tìm thấy sách.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new BookFormVM
        {
            Id = book.Id,
            CoverImageId = book.CoverImageId,
            Title = book.Title,
            AuthorsString = string.Join(", ", book.Authors),
            Publisher = book.Publisher,
            PublishYear = book.PublishYear,
            Price = book.Price,
            SelectedCategoryIds = book.CategoryIds,
            TotalQuantity = book.TotalQuantity,
            CurrentBorrowedQuantity = book.TotalQuantity - book.AvailableQuantity
        };

        await PopulateCategoryOptions(vm);
        return View(vm);
    }

    // POST: /Books/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BookFormVM model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoryOptions(model);
            return View(model);
        }

        var result = await _bookService.UpdateAsync(id, model);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message!);
            await PopulateCategoryOptions(model);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // POST: /Books/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _bookService.DeleteAsync(id);
        if (result.Success)
            TempData["Success"] = result.Message;
        else
            TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCategoryOptions(BookFormVM vm)
    {
        var categories = await _categoryService.GetAllWithBookCountAsync();
        vm.CategoryOptions = categories.Select(c => new SelectListItem
        {
            Value = c.Id,
            Text = c.Name,
            Selected = vm.SelectedCategoryIds.Contains(c.Id)
        }).ToList();
    }
}

