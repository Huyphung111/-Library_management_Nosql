using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Web.ViewModels.Account;

// UC01: form đăng nhập
public class LoginVM
{
    [Display(Name = "Tên đăng nhập")]
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Mật khẩu")]
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Trang người dùng định mở trước khi bị chuyển về đăng nhập.</summary>
    public string? ReturnUrl { get; set; }
}
