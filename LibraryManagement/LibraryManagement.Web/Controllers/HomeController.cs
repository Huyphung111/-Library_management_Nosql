using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Options;
using LibraryManagement.Web.Services;
using LibraryManagement.Web.ViewModels.Home;

namespace LibraryManagement.Web.Controllers;

public class HomeController : Controller
{
    // Chưa đăng nhập: hiện trang chủ công khai (tra cứu sách, banner).
    // Đã đăng nhập: chuyển hướng theo role (mục 2 thiết kế kỹ thuật).
    public async Task<IActionResult> Index(
        [FromServices] BookService books,
        [FromServices] CategoryService categories,
        [FromServices] IOptions<LibraryRules> rules)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole(nameof(Role.Librarian)))
                return RedirectToAction("Index", "Dashboard");
            return RedirectToAction("Index", "Catalog");
        }

        var vm = new BookShowcaseVM
        {
            Books = await books.GetAllAsync(),
            Categories = await categories.GetAllWithBookCountAsync(),
            Rules = rules.Value,
            IsGuest = true
        };
        return View("Landing", vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
