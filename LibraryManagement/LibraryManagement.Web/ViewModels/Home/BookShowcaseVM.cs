using LibraryManagement.Web.Options;
using LibraryManagement.Web.ViewModels.Books;
using LibraryManagement.Web.ViewModels.Categories;

namespace LibraryManagement.Web.ViewModels.Home;

/// <summary>
/// Khối trưng bày sách (Views/Shared/_BookShowcase.cshtml): cột danh mục, banner, lưới sách.
/// Dùng ở trang chủ khách (Home/Landing) và trang chủ độc giả (Catalog/Index). Dữ liệu lấy thật từ MongoDB.
/// </summary>
public class BookShowcaseVM
{
    public List<CategoryListVM> Categories { get; init; } = new();

    /// <summary>Sách mới thêm lên đầu. Ảnh bìa hiện qua /Covers/{CoverImageId}.</summary>
    public List<BookListVM> Books { get; init; } = new();

    /// <summary>Quy định (số ngày mượn, đặt giữ...) để chữ trên banner luôn khớp appsettings.json.</summary>
    public required LibraryRules Rules { get; init; }

    /// <summary>
    /// true: khách chưa đăng nhập, bấm sách mở popup và mời đăng nhập.
    /// false: độc giả, bấm sách đi tới trang chi tiết để đặt giữ / đặt trước.
    /// </summary>
    public bool IsGuest { get; init; }

    /// <summary>Từ khóa có sẵn khi mở trang (ví dụ /Catalog?search=abc).</summary>
    public string? InitialSearch { get; init; }

    /// <summary>Id sách yêu thích của độc giả (tô đỏ nút trái tim trên thẻ sách). Khách: rỗng.</summary>
    public HashSet<string> FavoriteBookIds { get; init; } = new();
}
