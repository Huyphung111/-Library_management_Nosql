using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Services;

namespace LibraryManagement.Web.Controllers;

/// <summary>
/// Sách yêu thích của Độc giả: đánh dấu sách để xem lại và đặt giữ / đặt trước nhanh.
/// </summary>
[Authorize(Roles = nameof(Role.Reader))]
public class MyFavoritesController : Controller
{
    private readonly FavoriteService _favorites;

    public MyFavoritesController(FavoriteService favorites) => _favorites = favorites;

    private string? ReaderId => User.FindFirstValue("ReaderId");

    // Nút trái tim gọi bằng fetch (trả JSON); form thường thì quay lại trang trước kèm thông báo
    private bool IsAjax => Request.Headers.XRequestedWith == "XMLHttpRequest";

    // GET: /MyFavorites
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (string.IsNullOrEmpty(ReaderId)) return Forbid();
        ViewData["Title"] = "Sách yêu thích";
        return View(await _favorites.GetListAsync(ReaderId));
    }

    // POST: /MyFavorites/Add
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string bookId, string? returnUrl)
    {
        if (string.IsNullOrEmpty(ReaderId)) return Forbid();
        return await Reply(await _favorites.AddAsync(ReaderId, bookId), favorite: true, returnUrl);
    }

    // POST: /MyFavorites/Remove
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string bookId, string? returnUrl)
    {
        if (string.IsNullOrEmpty(ReaderId)) return Forbid();
        return await Reply(await _favorites.RemoveAsync(ReaderId, bookId), favorite: false, returnUrl);
    }

    private async Task<IActionResult> Reply(ServiceResult result, bool favorite, string? returnUrl)
    {
        if (IsAjax)
            return result.Success
                ? Json(new { success = true, favorite, message = result.Message, count = (await _favorites.GetIdsAsync(ReaderId!)).Count })
                : BadRequest(new { success = false, message = result.Message });

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }
}
