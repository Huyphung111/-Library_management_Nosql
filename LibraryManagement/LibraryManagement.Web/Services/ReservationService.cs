using Microsoft.Extensions.Options;
using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Options;
using LibraryManagement.Web.ViewModels.Reservations;

namespace LibraryManagement.Web.Services;

/// <summary>
/// Một người đang chờ sách (yêu cầu đặt trước Approved), có sẵn thông tin liên hệ lấy từ readers.
/// Dùng ở UC08 bước 7a (A hiển thị) và UC10 bước 3a (B hiển thị). Chốt ngày 21/11/2026, phan-cong.md mục 3.
/// </summary>
public class WaitingReader
{
    public string ReservationId { get; init; } = string.Empty;
    public string BookId { get; init; } = string.Empty;
    public string BookTitle { get; init; } = string.Empty;
    public string ReaderId { get; init; } = string.Empty;
    public string CardNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }

    /// <summary>Thời điểm đặt (UTC). Danh sách sắp theo trường này tăng dần: người đặt sớm nhất lên đầu.</summary>
    public DateTime ReservedAt { get; init; }
}

/// <summary>
/// Nghiệp vụ UC10 (Xử lý đặt trước - Thủ thư) và UC16 (Đặt trước sách - Độc giả).
/// Kèm 3 hàm giao kèo hoàn chỉnh cho Member A gọi.
/// </summary>
public class ReservationService
{
    private readonly MongoDbContext _db;
    private readonly LoanService _loans;
    private readonly LibraryRules _rules;

    public ReservationService(MongoDbContext db, LoanService loans, IOptions<LibraryRules> rules)
    {
        _db = db;
        _loans = loans;
        _rules = rules.Value;
    }

    // Sửa lỗi 500 khi id sai định dạng ObjectId trên URL (A, 22/11/2026)
    private static bool ValidId(string? id) => MongoDB.Bson.ObjectId.TryParse(id, out _);

    /// <summary>Ngày cuối được đến nhận nếu đặt giữ hôm nay: HoldDays ngày, tính cả hôm nay (giờ VN).</summary>
    private DateTime HoldUntilVn() => DateHelper.TodayVn().AddDays(Math.Max(1, _rules.HoldDays) - 1);

    /// <summary>Có độc giả đang xếp hàng (Pending / Approved) chờ cuốn này không. Có thì không cho đặt giữ để khỏi chen hàng.</summary>
    private Task<bool> HasQueueAsync(string bookId) =>
        _db.Reservations.Find(r => r.BookId == bookId &&
            (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved)).AnyAsync();

    #region Đặt giữ sách (sách còn trên giá)

    /// <summary>
    /// Giữ 1 bản sách cho độc giả. Trong MỘT transaction: trừ availableQuantity bằng update có điều kiện "> 0"
    /// (hai người bấm cùng lúc cũng không giữ quá số bản có), rồi tạo yêu cầu trạng thái Held.
    /// </summary>
    private async Task<ServiceResult> CreateHoldAsync(Reader reader, Book book)
    {
        // Điều kiện giống lúc mượn: không có phiếu quá hạn, chưa chạm giới hạn số cuốn (tính cả sách đang giữ)
        var check = await _loans.CheckBorrowerAsync(reader.CardNumber);
        if (!check.Success) return ServiceResult.Fail(check.Message!);
        var borrower = check.Data!;

        if (borrower.OverdueLoans > 0)
            return ServiceResult.Fail($"Bạn đang có {borrower.OverdueLoans} phiếu mượn quá hạn. Vui lòng trả sách trước khi đặt giữ.");

        var held = borrower.HeldBookIds.Count;
        if (held >= _rules.MaxHolds)
            return ServiceResult.Fail($"Bạn đang được giữ {held} cuốn, tối đa {_rules.MaxHolds} cuốn. Hãy đến nhận hoặc hủy bớt trước khi đặt thêm.");

        if (borrower.BorrowingBooks + held >= _rules.MaxBooksPerReader)
            return ServiceResult.Fail($"Bạn đang mượn {borrower.BorrowingBooks} cuốn và được giữ {held} cuốn, đã đủ giới hạn {_rules.MaxBooksPerReader} cuốn.");

        var holdUntilVn = HoldUntilVn();
        var reservation = new Reservation
        {
            ReaderId = reader.Id!,
            BookId = book.Id!,
            BookTitle = book.Title,
            ReservedAt = DateTime.UtcNow,
            Status = ReservationStatus.Held,
            HoldUntil = DateHelper.VnDateToUtc(holdUntilVn)
        };

        try
        {
            using var session = await _db.Client.StartSessionAsync();
            await session.WithTransactionAsync(async (s, ct) =>
            {
                var dec = await _db.Books.UpdateOneAsync(s,
                    b => b.Id == book.Id && b.AvailableQuantity > 0,
                    Builders<Book>.Update.Inc(b => b.AvailableQuantity, -1),
                    cancellationToken: ct);
                if (dec.ModifiedCount == 0)
                    throw new BusinessException("Sách vừa được mượn hết. Bạn có thể đăng ký đặt trước để vào hàng chờ.");

                reservation.Id = null;   // nếu driver thử lại transaction thì tạo Id mới
                await _db.Reservations.InsertOneAsync(s, reservation, cancellationToken: ct);
                return true;
            });
        }
        catch (BusinessException ex)
        {
            return ServiceResult.Fail(ex.Message);
        }

        return ServiceResult.Ok(
            $"Thư viện đã giữ cuốn \"{book.Title}\" cho bạn đến hết ngày {holdUntilVn:dd/MM/yyyy}. Vui lòng mang thẻ độc giả đến quầy để nhận sách.");
    }

    /// <summary>
    /// Kết thúc một yêu cầu đang giữ sách (hủy hoặc hết hạn) và cộng lại 1 bản vào kho, trong MỘT transaction.
    /// Filter "status = Held" bảo đảm mỗi yêu cầu chỉ trả sách về kho đúng một lần.
    /// </summary>
    private async Task<bool> ReleaseHoldAsync(string reservationId, string bookId, ReservationStatus newStatus, string reason, string? processedBy)
    {
        using var session = await _db.Client.StartSessionAsync();
        return await session.WithTransactionAsync(async (s, ct) =>
        {
            var update = Builders<Reservation>.Update
                .Set(r => r.Status, newStatus)
                .Set(r => r.Reason, reason);
            if (processedBy != null) update = update.Set(r => r.ProcessedBy, processedBy);

            var res = await _db.Reservations.UpdateOneAsync(s,
                r => r.Id == reservationId && r.Status == ReservationStatus.Held,
                update, cancellationToken: ct);
            if (res.ModifiedCount == 0) return false;

            await _db.Books.UpdateOneAsync(s, b => b.Id == bookId,
                Builders<Book>.Update.Inc(b => b.AvailableQuantity, 1), cancellationToken: ct);
            return true;
        });
    }

    /// <summary>
    /// Hủy các yêu cầu đặt giữ đã quá ngày nhận (holdUntil trước hôm nay) và trả sách về kho.
    /// Gọi định kỳ từ HoldExpiryService và trước khi hiện danh sách. Trả về số yêu cầu đã hủy.
    /// </summary>
    public async Task<int> ExpireHoldsAsync()
    {
        var startOfToday = DateHelper.StartOfTodayVnUtc();
        var overdue = await _db.Reservations
            .Find(r => r.Status == ReservationStatus.Held && r.HoldUntil < startOfToday)
            .ToListAsync();

        var count = 0;
        foreach (var r in overdue)
        {
            if (await ReleaseHoldAsync(r.Id!, r.BookId, ReservationStatus.Expired,
                    "Quá hạn nhận sách, thư viện đã trả sách về kho.", null))
                count++;
        }
        return count;
    }

    /// <summary>Id các sách thư viện đang giữ cho độc giả (UC07 hiện sẵn khi lập phiếu).</summary>
    public Task<List<string>> GetHeldBookIdsAsync(string readerId) =>
        _db.Reservations.Find(r => r.ReaderId == readerId && r.Status == ReservationStatus.Held)
            .Project(r => r.BookId)
            .ToListAsync();

    /// <summary>
    /// UC07: độc giả đến nhận sách đã giữ → chuyển yêu cầu Held sang Completed trong transaction lập phiếu.
    /// true nghĩa là bản sách đã được trừ kho từ lúc đặt giữ, LoanService KHÔNG trừ lần nữa.
    /// false (vừa hết hạn / bị hủy) thì LoanService trừ kho như sách bình thường.
    /// </summary>
    public async Task<bool> ConsumeHoldAsync(IClientSessionHandle session, string readerId, string bookId, string loanId)
    {
        var result = await _db.Reservations.UpdateOneAsync(session,
            r => r.ReaderId == readerId && r.BookId == bookId && r.Status == ReservationStatus.Held,
            Builders<Reservation>.Update
                .Set(r => r.Status, ReservationStatus.Completed)
                .Set(r => r.CompletedLoanId, loanId));
        return result.ModifiedCount == 1;
    }

    #endregion

    #region UC16 - Phía Độc giả

    /// <summary>
    /// Lấy danh sách đặt trước của độc giả hiện tại, sắp theo thời gian đặt mới nhất lên đầu (UC16).
    /// </summary>
    public async Task<List<MyReservationItemVM>> GetMyReservationsAsync(string readerId)
    {
        await ExpireHoldsAsync();
        var reservations = await _db.Reservations
            .Find(r => r.ReaderId == readerId)
            .SortByDescending(r => r.ReservedAt)
            .ToListAsync();

        return reservations.Select(r => new MyReservationItemVM
        {
            Id = r.Id ?? string.Empty,
            BookId = r.BookId,
            BookTitle = r.BookTitle,
            ReservedAt = r.ReservedAt,
            Status = r.Status,
            Reason = r.Reason,
            HoldUntil = r.HoldUntil
        }).ToList();
    }

    /// <summary>
    /// Lấy thông tin sách để hiển thị màn hình xác nhận đặt trước (UC16 bước 3).
    /// </summary>
    public async Task<ReservationConfirmVM?> GetBookForReservationConfirmAsync(string bookId)
    {
        if (!ValidId(bookId)) return null;
        var book = await _db.Books.Find(b => b.Id == bookId).FirstOrDefaultAsync();
        if (book == null) return null;

        var categories = await _db.Categories
            .Find(c => book.CategoryIds.Contains(c.Id!))
            .Project(c => c.Name)
            .ToListAsync();

        var hasQueue = await HasQueueAsync(bookId);
        return new ReservationConfirmVM
        {
            BookId = book.Id ?? string.Empty,
            BookTitle = book.Title,
            Authors = string.Join(", ", book.Authors),
            Publisher = book.Publisher ?? string.Empty,
            PublishYear = book.PublishYear,
            CategoryNames = categories,
            AvailableQuantity = book.AvailableQuantity,
            HasQueue = hasQueue,
            WillHold = book.AvailableQuantity > 0 && !hasQueue,
            HoldUntilPreview = HoldUntilVn(),
            HoldDays = _rules.HoldDays,
            MaxHolds = _rules.MaxHolds
        };
    }

    /// <summary>
    /// Độc giả gửi yêu cầu cho một cuốn sách (UC16). Tuân thủ QĐ08:
    /// - Thẻ độc giả phải đang Hoạt động và chưa hết hạn.
    /// - Độc giả chưa có yêu cầu Chờ duyệt / Đã duyệt / Đang giữ cho cuốn sách này, và không đang mượn cuốn này.
    /// - Sách còn và không ai xếp hàng chờ → ĐẶT GIỮ: giữ 1 bản đến hết HoldDays ngày (CreateHoldAsync).
    /// - Sách hết, hoặc còn nhưng đã có người chờ trước → ĐẶT TRƯỚC: vào hàng chờ, Thủ thư duyệt và liên hệ.
    /// </summary>
    public async Task<ServiceResult> CreateAsync(string readerId, string bookId)
    {
        if (!ValidId(bookId)) return ServiceResult.Fail("Không tìm thấy thông tin cuốn sách trong hệ thống.");
        // 1. Kiểm tra thông tin thẻ độc giả (UC16 bước 2, dòng phụ 2a)
        var reader = await _db.Readers.Find(r => r.Id == readerId).FirstOrDefaultAsync();
        if (reader == null)
            return ServiceResult.Fail("Không tìm thấy thông tin thẻ độc giả của bạn. Vui lòng liên hệ thủ thư.");

        if (reader.CardStatus == CardStatus.Locked)
        {
            var reason = string.IsNullOrWhiteSpace(reader.LockReason) ? "" : $" (lý do: {reader.LockReason})";
            return ServiceResult.Fail($"Thẻ độc giả của bạn đang bị khóa{reason}. Bạn chưa thể đặt trước sách, vui lòng liên hệ thủ thư.");
        }

        if (reader.IsExpired)
        {
            return ServiceResult.Fail($"Thẻ thư viện của bạn đã hết hạn ngày {DateHelper.ToVnDate(reader.ExpiryDate):dd/MM/yyyy}. Vui lòng liên hệ thủ thư để gia hạn thẻ.");
        }

        // 2. Kiểm tra sách và tình trạng kho (UC16 bước 1a, 2)
        var book = await _db.Books.Find(b => b.Id == bookId).FirstOrDefaultAsync();
        if (book == null)
            return ServiceResult.Fail("Không tìm thấy thông tin cuốn sách trong hệ thống.");

        // 3. Kiểm tra yêu cầu còn hiệu lực (QĐ08: mỗi độc giả chỉ có 1 yêu cầu Chờ duyệt / Đã duyệt / Đang giữ cho mỗi cuốn)
        var hasActiveReservation = await _db.Reservations.Find(r =>
            r.ReaderId == readerId &&
            r.BookId == bookId &&
            (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Held)
        ).AnyAsync();

        if (hasActiveReservation)
        {
            return ServiceResult.Fail("Bạn đã có yêu cầu cho cuốn sách này (đang chờ duyệt, đã duyệt hoặc đang được giữ). Không thể đặt thêm.");
        }

        // 4. Kiểm tra độc giả có đang mượn sách này không (UC16 bước 2)
        if (await _loans.IsBorrowingAsync(readerId, bookId))
        {
            return ServiceResult.Fail("Bạn đang mượn một cuốn sách này, không thể đặt thêm.");
        }

        // 5. Sách còn và không ai xếp hàng trước → đặt giữ
        var hasQueue = await HasQueueAsync(bookId);
        if (book.AvailableQuantity > 0 && !hasQueue)
            return await CreateHoldAsync(reader, book);

        // 6. Sách hết (hoặc còn nhưng đã có người chờ trước) → tạo yêu cầu đặt trước (UC16 bước 5)
        var reservation = new Reservation
        {
            ReaderId = readerId,
            BookId = bookId,
            BookTitle = book.Title,
            ReservedAt = DateTime.UtcNow,
            Status = ReservationStatus.Pending
        };

        await _db.Reservations.InsertOneAsync(reservation);
        return ServiceResult.Ok(hasQueue && book.AvailableQuantity > 0
            ? "Cuốn sách này đang có độc giả khác xếp hàng chờ trước, yêu cầu của bạn đã được đưa vào hàng chờ. Thư viện sẽ liên hệ khi đến lượt bạn."
            : "Đã gửi yêu cầu đặt trước sách thành công. Thư viện sẽ liên hệ bạn khi có sách về kho.");
    }

    /// <summary>
    /// Độc giả tự hủy yêu cầu đặt trước sách của chính mình (UC16 bước 1b).
    /// </summary>
    public async Task<ServiceResult> CancelByReaderAsync(string readerId, string reservationId)
    {
        if (!ValidId(reservationId)) return ServiceResult.Fail("Không tìm thấy yêu cầu đặt trước của bạn.");
        var res = await _db.Reservations.Find(r => r.Id == reservationId).FirstOrDefaultAsync();
        if (res == null || res.ReaderId != readerId)
            return ServiceResult.Fail("Không tìm thấy yêu cầu đặt trước của bạn.");

        if (res.Status == ReservationStatus.Held)
        {
            return await ReleaseHoldAsync(res.Id!, res.BookId, ReservationStatus.Cancelled, "Độc giả tự hủy đặt giữ", null)
                ? ServiceResult.Ok("Đã hủy đặt giữ, cuốn sách đã được trả lại kho.")
                : ServiceResult.Fail("Yêu cầu vừa thay đổi trạng thái (đã nhận sách hoặc hết hạn), vui lòng tải lại trang.");
        }

        if (res.Status != ReservationStatus.Pending && res.Status != ReservationStatus.Approved)
            return ServiceResult.Fail("Chỉ có thể hủy yêu cầu đang ở trạng thái Chờ duyệt, Đã duyệt hoặc Đang giữ sách.");

        var update = Builders<Reservation>.Update
            .Set(r => r.Status, ReservationStatus.Cancelled)
            .Set(r => r.Reason, "Độc giả tự hủy yêu cầu");

        await _db.Reservations.UpdateOneAsync(r => r.Id == reservationId, update);
        return ServiceResult.Ok("Đã hủy yêu cầu đặt trước sách thành công.");
    }

    #endregion

    #region UC10 - Phía Thủ thư

    /// <summary>
    /// Lấy danh sách yêu cầu đặt trước cho Thủ thư theo tab quản lý (UC10).
    /// </summary>
    public async Task<LibrarianReservationListVM> GetLibrarianListAsync(string tab = "all")
    {
        await ExpireHoldsAsync();
        var reservations = await _db.Reservations.Find(_ => true)
            .SortByDescending(r => r.ReservedAt)
            .ToListAsync();

        var readerIds = reservations.Select(r => r.ReaderId).Distinct().ToList();
        var bookIds = reservations.Select(r => r.BookId).Distinct().ToList();

        var readers = await _db.Readers.Find(r => readerIds.Contains(r.Id!)).ToListAsync();
        var readerMap = readers.ToDictionary(r => r.Id!);

        var books = await _db.Books.Find(b => bookIds.Contains(b.Id!)).ToListAsync();
        var bookMap = books.ToDictionary(b => b.Id!);

        var items = reservations.Select(r =>
        {
            readerMap.TryGetValue(r.ReaderId, out var reader);
            bookMap.TryGetValue(r.BookId, out var book);

            return new LibrarianReservationVM
            {
                Id = r.Id ?? string.Empty,
                ReaderId = r.ReaderId,
                ReaderName = reader?.FullName ?? "Độc giả không tồn tại",
                CardNumber = reader?.CardNumber ?? "---",
                ReaderPhone = reader?.Phone,
                ReaderEmail = reader?.Email,
                CardStatus = reader?.CardStatus ?? CardStatus.Active,
                IsCardExpired = reader?.IsExpired ?? false,
                BookId = r.BookId,
                BookTitle = r.BookTitle,
                ReservedAt = r.ReservedAt,
                Status = r.Status,
                Reason = r.Reason,
                ProcessedBy = r.ProcessedBy,
                AvailableQuantity = book?.AvailableQuantity ?? 0,
                HoldUntil = r.HoldUntil
            };
        }).ToList();

        var vm = new LibrarianReservationListVM
        {
            CurrentTab = tab.ToLower(),
            TotalCount = items.Count,
            PendingCount = items.Count(x => x.Status == ReservationStatus.Pending),
            ApprovedCount = items.Count(x => x.Status == ReservationStatus.Approved),
            ReadyCount = items.Count(x => x.IsReadyForPickup),
            HeldCount = items.Count(x => x.Status == ReservationStatus.Held),
            CompletedCount = items.Count(x => x.Status == ReservationStatus.Completed),
            CancelledCount = items.Count(x => x.Status is ReservationStatus.Cancelled or ReservationStatus.Rejected or ReservationStatus.Expired)
        };

        // Lọc danh sách theo tab được chọn
        vm.Items = vm.CurrentTab switch
        {
            "pending" => items.Where(x => x.Status == ReservationStatus.Pending).ToList(),
            "approved" => items.Where(x => x.Status == ReservationStatus.Approved).ToList(),
            "ready" => items.Where(x => x.IsReadyForPickup).OrderBy(x => x.ReservedAt).ToList(), // Người đặt trước lên đầu
            "held" => items.Where(x => x.Status == ReservationStatus.Held).OrderBy(x => x.HoldUntil).ToList(), // sắp hết hạn lên đầu
            "completed" => items.Where(x => x.Status == ReservationStatus.Completed).ToList(),
            "cancelled" => items.Where(x => x.Status is ReservationStatus.Cancelled or ReservationStatus.Rejected or ReservationStatus.Expired).ToList(),
            _ => items
        };

        return vm;
    }

    /// <summary>
    /// Thủ thư duyệt yêu cầu đặt trước (UC10 bước 5).
    /// </summary>
    public async Task<ServiceResult> ApproveAsync(string id, string librarianId)
    {
        if (!ValidId(id)) return ServiceResult.Fail("Không tìm thấy yêu cầu đặt trước.");
        var res = await _db.Reservations.Find(r => r.Id == id).FirstOrDefaultAsync();
        if (res == null)
            return ServiceResult.Fail("Không tìm thấy yêu cầu đặt trước.");

        if (res.Status != ReservationStatus.Pending)
            return ServiceResult.Fail("Chỉ có thể duyệt yêu cầu đang ở trạng thái Chờ duyệt.");

        var update = Builders<Reservation>.Update
            .Set(r => r.Status, ReservationStatus.Approved)
            .Set(r => r.ProcessedBy, librarianId);

        await _db.Reservations.UpdateOneAsync(r => r.Id == id, update);
        return ServiceResult.Ok("Đã duyệt yêu cầu đặt trước thành công.");
    }

    /// <summary>
    /// Thủ thư từ chối yêu cầu đặt trước kèm lý do (UC10 bước 5a).
    /// </summary>
    public async Task<ServiceResult> RejectAsync(string id, string librarianId, string reason)
    {
        if (!ValidId(id)) return ServiceResult.Fail("Không tìm thấy yêu cầu đặt trước.");
        if (string.IsNullOrWhiteSpace(reason))
            return ServiceResult.Fail("Vui lòng nhập lý do từ chối yêu cầu đặt trước.", "Reason");

        var res = await _db.Reservations.Find(r => r.Id == id).FirstOrDefaultAsync();
        if (res == null)
            return ServiceResult.Fail("Không tìm thấy yêu cầu đặt trước.");

        if (res.Status != ReservationStatus.Pending)
            return ServiceResult.Fail("Chỉ có thể từ chối yêu cầu đang ở trạng thái Chờ duyệt.");

        var update = Builders<Reservation>.Update
            .Set(r => r.Status, ReservationStatus.Rejected)
            .Set(r => r.Reason, reason.Trim())
            .Set(r => r.ProcessedBy, librarianId);

        await _db.Reservations.UpdateOneAsync(r => r.Id == id, update);
        return ServiceResult.Ok("Đã từ chối yêu cầu đặt trước.");
    }

    /// <summary>
    /// Thủ thư hủy một yêu cầu đặt trước đã duyệt kèm lý do (UC10 bước 3b, ví dụ liên hệ không được).
    /// </summary>
    public async Task<ServiceResult> CancelByLibrarianAsync(string id, string librarianId, string reason)
    {
        if (!ValidId(id)) return ServiceResult.Fail("Không tìm thấy yêu cầu đặt trước.");
        if (string.IsNullOrWhiteSpace(reason))
            return ServiceResult.Fail("Vui lòng nhập lý do hủy yêu cầu đặt trước.", "Reason");

        var res = await _db.Reservations.Find(r => r.Id == id).FirstOrDefaultAsync();
        if (res == null)
            return ServiceResult.Fail("Không tìm thấy yêu cầu đặt trước.");

        if (res.Status == ReservationStatus.Held)
        {
            return await ReleaseHoldAsync(res.Id!, res.BookId, ReservationStatus.Cancelled, reason.Trim(), librarianId)
                ? ServiceResult.Ok("Đã hủy đặt giữ, cuốn sách đã được trả lại kho.")
                : ServiceResult.Fail("Yêu cầu vừa thay đổi trạng thái (đã nhận sách hoặc hết hạn), vui lòng tải lại trang.");
        }

        if (res.Status != ReservationStatus.Approved)
            return ServiceResult.Fail("Chỉ có thể hủy yêu cầu đang ở trạng thái Đã duyệt hoặc Đang giữ sách.");

        var update = Builders<Reservation>.Update
            .Set(r => r.Status, ReservationStatus.Cancelled)
            .Set(r => r.Reason, reason.Trim())
            .Set(r => r.ProcessedBy, librarianId);

        await _db.Reservations.UpdateOneAsync(r => r.Id == id, update);
        return ServiceResult.Ok("Đã hủy yêu cầu đặt trước.");
    }

    #endregion

    #region 3 Hàm giao kèo cho Thành viên A (phan-cong.md mục 3)

    /// <summary>
    /// UC07: Khi độc giả lập phiếu mượn các sách này, tự động chuyển các yêu cầu đặt trước Pending/Approved
    /// tương ứng của độc giả sang Completed, ghi completedLoanId.
    /// Chạy trong session (transaction) do A truyền vào. Trả về số yêu cầu đã chuyển.
    /// </summary>
    public async Task<long> CompleteForLoanAsync(
        IClientSessionHandle session, string readerId, IEnumerable<string> bookIds, string loanId)
    {
        var bookIdList = bookIds.ToList();
        if (bookIdList.Count == 0) return 0;

        var filter = Builders<Reservation>.Filter.And(
            Builders<Reservation>.Filter.Eq(r => r.ReaderId, readerId),
            Builders<Reservation>.Filter.In(r => r.BookId, bookIdList),
            Builders<Reservation>.Filter.In(r => r.Status, new[] { ReservationStatus.Pending, ReservationStatus.Approved })
        );

        var update = Builders<Reservation>.Update
            .Set(r => r.Status, ReservationStatus.Completed)
            .Set(r => r.CompletedLoanId, loanId);

        var result = await _db.Reservations.UpdateManyAsync(session, filter, update);
        return result.ModifiedCount;
    }

    /// <summary>
    /// UC08: Khi sách được trả về kho, lấy danh sách người đang chờ các sách vừa trả (yêu cầu Approved),
    /// sắp xếp theo thời điểm đặt ReservedAt tăng dần (người đặt trước lên đầu).
    /// Kèm thông tin họ tên, mã thẻ, SĐT, email lấy từ readers để Thủ thư liên hệ.
    /// </summary>
    public async Task<List<WaitingReader>> GetWaitingListAsync(IEnumerable<string> bookIds)
    {
        var bookIdList = bookIds.ToList();
        if (bookIdList.Count == 0) return new List<WaitingReader>();

        var filter = Builders<Reservation>.Filter.And(
            Builders<Reservation>.Filter.In(r => r.BookId, bookIdList),
            Builders<Reservation>.Filter.Eq(r => r.Status, ReservationStatus.Approved)
        );

        var reservations = await _db.Reservations.Find(filter)
            .SortBy(r => r.ReservedAt)
            .ToListAsync();

        if (reservations.Count == 0) return new List<WaitingReader>();

        var readerIds = reservations.Select(r => r.ReaderId).Distinct().ToList();
        var readers = await _db.Readers.Find(r => readerIds.Contains(r.Id!)).ToListAsync();
        var readerMap = readers.ToDictionary(r => r.Id!);

        return reservations.Select(res =>
        {
            readerMap.TryGetValue(res.ReaderId, out var reader);
            return new WaitingReader
            {
                ReservationId = res.Id ?? string.Empty,
                BookId = res.BookId,
                BookTitle = res.BookTitle,
                ReaderId = res.ReaderId,
                CardNumber = reader?.CardNumber ?? string.Empty,
                FullName = reader?.FullName ?? string.Empty,
                Phone = reader?.Phone,
                Email = reader?.Email,
                ReservedAt = res.ReservedAt
            };
        }).ToList();
    }

    /// <summary>
    /// UC09, UC15: Kiểm tra các sách này có người đang chờ (yêu cầu Pending hoặc Approved) không.
    /// Nếu có người đang chờ, hệ thống chặn không cho gia hạn theo quy định QĐ04.
    /// </summary>
    public async Task<bool> HasWaitingAsync(IEnumerable<string> bookIds)
    {
        var bookIdList = bookIds.ToList();
        if (bookIdList.Count == 0) return false;

        var filter = Builders<Reservation>.Filter.And(
            Builders<Reservation>.Filter.In(r => r.BookId, bookIdList),
            Builders<Reservation>.Filter.In(r => r.Status, new[] { ReservationStatus.Pending, ReservationStatus.Approved })
        );

        return await _db.Reservations.Find(filter).AnyAsync();
    }

    #endregion
}
