using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using LibraryManagement.Web.Services;
using LibraryManagement.Web.ViewModels.Search;

namespace LibraryManagement.Web.Controllers;

/// <summary>
/// UC11: Tìm kiếm đa năng phía Thủ thư (Sách, Độc giả, Phiếu mượn).
/// </summary>
[Authorize(Roles = "Librarian")]
public class SearchController : Controller
{
    private readonly SearchService _search;

    public SearchController(SearchService search)
    {
        _search = search;
    }

    /// <summary>
    /// Trang tra cứu chung, tham số type = "book" / "reader" / "loan".
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(SearchFilterVM filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Type))
            filter.Type = "book";

        var vm = new SearchIndexVM
        {
            Filter = filter
        };

        // Lấy danh mục thể loại để nạp dropdown
        var categories = await _search.GetCategoriesAsync();
        vm.Categories = categories.Select(c => new SelectListItem
        {
            Value = c.Id,
            Text = c.Name,
            Selected = (c.Id == filter.CategoryId)
        }).ToList();

        // Kiểm tra xem người dùng có truyền tiêu chí tìm kiếm không
        vm.HasSearched = !string.IsNullOrWhiteSpace(filter.Keyword)
            || !string.IsNullOrWhiteSpace(filter.CategoryId)
            || !string.IsNullOrWhiteSpace(filter.LoanStatus)
            || filter.FromDate.HasValue
            || filter.ToDate.HasValue;

        switch (filter.Type.ToLower())
        {
            case "reader":
                ViewData["Title"] = "Tra cứu độc giả";
                vm.Readers = await _search.SearchReadersAsync(filter.Keyword);
                break;

            case "loan":
                ViewData["Title"] = "Tra cứu phiếu mượn";
                vm.Loans = await _search.SearchLoansAsync(
                    filter.Keyword, filter.LoanStatus, filter.FromDate, filter.ToDate);
                break;

            default:
                filter.Type = "book";
                ViewData["Title"] = "Tra cứu sách";
                vm.Books = await _search.SearchBooksAsync(filter.Keyword, filter.CategoryId);
                break;
        }

        return View(vm);
    }
}
