using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Options;
using LibraryManagement.Web.Services;
using LibraryManagement.Web.ViewModels.Books;
using LibraryManagement.Web.ViewModels.Home;

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

    // GET: /Catalog — trang chủ độc giả theo thiết kế Antigravity (partial _BookShowcase), giữ menu bên trái của layout.
    // Tải toàn bộ sách một lần, lọc theo thể loại / từ khóa ngay trên trình duyệt; ?search= vẫn dùng được làm từ khóa ban đầu.
    [HttpGet]
    public async Task<IActionResult> Index(string? search, [FromServices] IOptions<LibraryRules> rules)
    {
        var vm = new BookShowcaseVM
        {
            Books = await _bookService.GetAllAsync(),
            Categories = await _categoryService.GetAllWithBookCountAsync(),
            Rules = rules.Value,
            IsGuest = false,
            InitialSearch = search
        };
        return View(vm);
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

