using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Web.Services;

namespace LibraryManagement.Web.Controllers;

// UC04, UC13: trả ảnh bìa sách lưu trong GridFS (mục 7.5 thiết kế kỹ thuật).
// Dùng chung cho Thủ thư (Quản lý sách) và Độc giả (Tìm sách), nên chỉ yêu cầu đã đăng nhập.
[Authorize]
public class CoversController : Controller
{
    private readonly BookService _books;

    public CoversController(BookService books) => _books = books;

    // GET /Covers/{id}
    [HttpGet("Covers/{id}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]   // id ảnh không đổi: đổi ảnh là id mới
    public async Task<IActionResult> Get(string id)
    {
        var cover = await _books.OpenCoverAsync(id);
        if (cover == null) return NotFound();
        return File(cover.Value.Stream, cover.Value.ContentType);
    }
}
