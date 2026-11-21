namespace LibraryManagement.Web.Services;

/// <summary>
/// Kết quả service trả về: thành công hoặc lỗi kèm thông báo tiếng Việt.
/// Controller đưa Message vào TempData["Success"] / TempData["Error"] để _Alert hiển thị.
/// </summary>
public class ServiceResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    /// <summary>
    /// Tên property của ViewModel bị lỗi, khi cần "báo lỗi tại trường tương ứng".
    /// Controller dùng: ModelState.AddModelError(result.Field ?? string.Empty, result.Message!).
    /// Null nghĩa là lỗi chung, hiện ở ValidationSummary.
    /// </summary>
    public string? Field { get; init; }

    public static ServiceResult Ok(string? message = null) => new() { Success = true, Message = message };
    public static ServiceResult Fail(string message, string? field = null) =>
        new() { Success = false, Message = message, Field = field };
}

/// <summary>Kết quả có kèm dữ liệu trả về.</summary>
public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public new static ServiceResult<T> Fail(string message, string? field = null) =>
        new() { Success = false, Message = message, Field = field };
}

/// <summary>
/// Ném ra khi vi phạm quy định nghiệp vụ ở nơi khó trả ServiceResult
/// (ví dụ trong transaction, để transaction tự hủy). Service bắt lại và đổi thành ServiceResult.Fail.
/// </summary>
public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}
