using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Web.Services;
using LibraryManagement.Web.ViewModels.Account;

namespace LibraryManagement.Web.Controllers;

// UC01–UC03, dùng chung cho Thủ thư và Độc giả.
public class AccountController : Controller
{
    private readonly AuthService _auth;

    public AccountController(AuthService auth) => _auth = auth;

    // UC01
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["Title"] = "Đăng nhập";
        return View(new LoginVM { ReturnUrl = returnUrl });
    }

    // UC01
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVM model)
    {
        ViewData["Title"] = "Đăng nhập";
        if (!ModelState.IsValid)
            return View(model);   // bước 3a: bỏ trống trường

        var result = await _auth.LoginAsync(model.Username, model.Password);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message!);   // bước 4a
            model.Password = string.Empty;
            return View(model);
        }

        var login = result.Data!;
        var user = login.User;

        // Claim theo mục 6.3 thiết kế kỹ thuật
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id!),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        if (!string.IsNullOrEmpty(user.ReaderId))
            claims.Add(new Claim("ReaderId", user.ReaderId));
        if (!string.IsNullOrEmpty(login.CardNotice))
            claims.Add(new Claim("CardNotice", login.CardNotice));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        // Bước 5: về trang đang định mở, hoặc trang chính theo vai trò (HomeController chuyển hướng)
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    // UC02
    // Không dùng [ValidateAntiForgeryToken] ở đây vì bước 1a: khi phiên đã hết hạn, token chống giả mạo
    // gắn với người dùng cũ sẽ không còn khớp và trả lỗi 400. Vẫn kiểm tra token khi còn đăng nhập.
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout([FromServices] IAntiforgery antiforgery)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!await antiforgery.IsRequestValidAsync(HttpContext))
                return BadRequest();

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        return RedirectToAction(nameof(Login));   // bước 3, và bước 1a khi phiên đã hết hạn
    }

    // UC03
    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        ViewData["Title"] = "Đổi mật khẩu";
        return View(new ChangePasswordVM());
    }

    // UC03
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordVM model)
    {
        ViewData["Title"] = "Đổi mật khẩu";
        if (!ModelState.IsValid)
            return View(model);   // bước 4b: trống hoặc xác nhận không khớp

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _auth.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message!);   // bước 4a, 4b
            return View(new ChangePasswordVM());   // không trả lại mật khẩu đã gõ
        }

        TempData["Success"] = result.Message;   // bước 5
        return RedirectToAction(nameof(ChangePassword));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Không có quyền truy cập";
        return View();
    }
}
