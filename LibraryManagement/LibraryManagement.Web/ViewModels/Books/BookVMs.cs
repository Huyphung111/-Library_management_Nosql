using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LibraryManagement.Web.ViewModels.Books;

/// <summary>
/// Hiển thị một dòng sách trong bảng danh sách sách (UC04).
/// </summary>
public class BookListVM
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public int? PublishYear { get; set; }
    public long Price { get; set; }
    public List<string> CategoryNames { get; set; } = new();
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int BorrowedQuantity => TotalQuantity - AvailableQuantity;
}

/// <summary>
/// Dữ liệu form Thêm / Sửa sách (UC04).
/// </summary>
public class BookFormVM
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sách")]
    [StringLength(200, ErrorMessage = "Tên sách không được vượt quá 200 ký tự")]
    [Display(Name = "Tên sách")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập ít nhất một tác giả")]
    [Display(Name = "Tác giả (cách nhau bởi dấu phẩy)")]
    public string AuthorsString { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "Nhà xuất bản không được vượt quá 150 ký tự")]
    [Display(Name = "Nhà xuất bản")]
    public string? Publisher { get; set; }

    [Range(1000, 2100, ErrorMessage = "Năm xuất bản không hợp lệ")]
    [Display(Name = "Năm xuất bản")]
    public int? PublishYear { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giá sách")]
    [Range(1000, 100_000_000, ErrorMessage = "Giá sách phải lớn hơn 1.000đ")]
    [Display(Name = "Giá sách (VNĐ)")]
    public long Price { get; set; }

    [Display(Name = "Thể loại")]
    public List<string> SelectedCategoryIds { get; set; } = new();

    [Required(ErrorMessage = "Vui lòng nhập số lượng tổng")]
    [Range(1, 10000, ErrorMessage = "Số lượng tổng phải lớn hơn 0")]
    [Display(Name = "Số lượng tổng")]
    public int TotalQuantity { get; set; } = 1;

    /// <summary>Dùng khi chỉnh sửa để hiển thị số lượng đang cho mượn.</summary>
    public int CurrentBorrowedQuantity { get; set; }

    /// <summary>Danh sách thể loại để hiển thị checkbox lựa chọn.</summary>
    public List<SelectListItem> CategoryOptions { get; set; } = new();
}

