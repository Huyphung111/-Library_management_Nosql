using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Web.Models;

namespace LibraryManagement.Web.Controllers;

public class HomeController : Controller
{
    // Chuyển hướng theo role sau đăng nhập (mục 2 thiết kế kỹ thuật).
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        if (User.IsInRole(nameof(Role.Librarian)))
            return RedirectToAction("Index", "Dashboard");

        return RedirectToAction("Index", "Catalog");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
