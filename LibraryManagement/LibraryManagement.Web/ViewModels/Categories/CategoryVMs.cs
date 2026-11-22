using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Web.ViewModels.Categories;

/// <summary>
/// Hiển thị 1 dòng trong bảng danh sách thể loại (UC05).
/// </summary>
public class CategoryListVM
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BookCount { get; set; }
}

/// <summary>
/// Dữ liệu cho form Thêm / Sửa thể loại (UC05).
/// </summary>
public class CategoryFormVM
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên thể loại")]
    [StringLength(100, ErrorMessage = "Tên thể loại không được vượt quá 100 ký tự")]
    [Display(Name = "Tên thể loại")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }
}

