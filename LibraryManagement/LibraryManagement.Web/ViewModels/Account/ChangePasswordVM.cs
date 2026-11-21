using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Web.ViewModels.Account;

// UC03: form đổi mật khẩu.
// Độ dài tối thiểu và "khác mật khẩu hiện tại" kiểm tra trong AuthService (đọc từ LibraryRules, không viết cứng).
public class ChangePasswordVM
{
    [Display(Name = "Mật khẩu hiện tại")]
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Display(Name = "Mật khẩu mới")]
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Display(Name = "Xác nhận mật khẩu mới")]
    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu không khớp với mật khẩu mới.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
