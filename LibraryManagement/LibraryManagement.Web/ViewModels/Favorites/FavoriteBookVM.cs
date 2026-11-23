using LibraryManagement.Web.Models;

namespace LibraryManagement.Web.ViewModels.Favorites;

/// <summary>Một cuốn trên trang "Sách yêu thích" của độc giả.</summary>
public class FavoriteBookVM
{
    public string BookId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public string? FirstCategory { get; set; }
    public string? CoverImageId { get; set; }
    public int AvailableQuantity { get; set; }
    public int TotalQuantity { get; set; }

    /// <summary>Yêu cầu đặt giữ / đặt trước còn hiệu lực của chính độc giả cho cuốn này (null nếu chưa có).</summary>
    public ReservationStatus? ActiveReservation { get; set; }
}
