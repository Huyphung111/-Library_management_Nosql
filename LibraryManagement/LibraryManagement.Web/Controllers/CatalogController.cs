using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Services;
using LibraryManagement.Web.ViewModels.Books;

namespace LibraryManagement.Web.Controllers;

/// <summary>
/// UC13: Tra cứu sách dành cho Độc giả (Member B).
/// </summary>
[Authorize(Roles = nameof(Role.Reader))]
public class CatalogController : Controller
{
    private readonly BookService _bookService;
    private readonly CategoryService _categoryService;

    public CatalogController(BookService bookService, CategoryService categoryService)
    {
        _bookService = bookService;
        _categoryService = categoryService;
    }

    // GET: /Catalog
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

    // GET: /Catalog/Details/{id}
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var book = await _bookService.GetByIdAsync(id);
        if (book == null)
        {
            TempData["Error"] = "Không tìm thấy cuốn sách yêu cầu.";
            return RedirectToAction(nameof(Index));
        }

        // Lấy tên các thể loại của sách
        var allCategories = await _categoryService.GetAllWithBookCountAsync();
        var catMap = allCategories.ToDictionary(c => c.Id, c => c.Name);

        var vm = new BookListVM
        {
            Id = book.Id!,
            Title = book.Title,
            Authors = string.Join(", ", book.Authors),
            Publisher = book.Publisher ?? "Đang cập nhật",
            PublishYear = book.PublishYear,
            Price = book.Price,
            CategoryNames = book.CategoryIds.Where(id => catMap.ContainsKey(id)).Select(id => catMap[id]).ToList(),
            TotalQuantity = book.TotalQuantity,
            AvailableQuantity = book.AvailableQuantity,
            CoverImageId = book.CoverImageId
        };

        return View(vm);
    }
}

