using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Options;
using LibraryManagement.Web.ViewModels.Loans;

namespace LibraryManagement.Web.Services;

// UC07, UC08, UC09, UC14, UC15: mượn trả, gia hạn. (A viết ở đợt 2 và 3)
public class LoanService
{
    private readonly MongoDbContext _db;
    private readonly LibraryRules _rules;
    private readonly CounterService _counters;

    // Lazy để tránh vòng phụ thuộc: ReservationService cũng inject LoanService (UC16 gọi IsBorrowingAsync).
    // Dùng _reservations.Value khi cần gọi CompleteForLoanAsync, GetWaitingListAsync, HasWaitingAsync.
    private readonly Lazy<ReservationService> _reservations;

    public LoanService(
        MongoDbContext db,
        IOptions<LibraryRules> rules,
        CounterService counters,
        Lazy<ReservationService> reservations)
    {
        _db = db;
        _rules = rules.Value;
        _counters = counters;
        _reservations = reservations;
    }

    // ------------------------------------------------------------------
    // Dùng chung
    // ------------------------------------------------------------------

    private static bool ValidId(string? id) => ObjectId.TryParse(id, out _);

    public async Task<Loan?> GetByIdAsync(string? id)
    {
        if (!ValidId(id)) return null;
        return await _db.Loans.Find(l => l.Id == id).FirstOrDefaultAsync();
    }

    /// <summary>Phiếu quá hạn: Borrowing và dueDate trước 00:00 hôm nay giờ VN (tính khi truy vấn).</summary>
    private static FilterDefinition<Loan> OverdueFilter(string readerId) =>
        Builders<Loan>.Filter.Eq(l => l.Reader.ReaderId, readerId)
        & Builders<Loan>.Filter.Eq(l => l.Status, LoanStatus.Borrowing)
        & Builders<Loan>.Filter.Lt(l => l.DueDate, DateHelper.StartOfTodayVnUtc());

    /// <summary>
    /// Số cuốn độc giả đang mượn (chưa trả) trên mọi phiếu: $match → $unwind items → $match chưa trả → $count
    /// (mục 7.3 thiết kế kỹ thuật).
    /// </summary>
    public async Task<int> CountBorrowingBooksAsync(string readerId)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "reader.readerId", ObjectId.Parse(readerId) },
                { "status", LoanStatus.Borrowing.ToString() }
            }),
            new BsonDocument("$unwind", "$items"),
            new BsonDocument("$match", new BsonDocument("items.returnDate", BsonNull.Value)),
            new BsonDocument("$count", "n")
        };
        var result = await _db.Loans.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();
        return result?["n"].ToInt32() ?? 0;
    }

    /// <summary>Lý do thẻ không dùng được để mượn / gia hạn (QĐ04, QĐ07). Rỗng nghĩa là thẻ dùng được.</summary>
    private static List<string> CardProblems(Reader reader)
    {
        var reasons = new List<string>();
        if (reader.CardStatus == CardStatus.Locked)
            reasons.Add(string.IsNullOrWhiteSpace(reader.LockReason)
                ? "Thẻ đang bị khóa."
                : $"Thẻ đang bị khóa (lý do: {reader.LockReason}).");
        if (reader.IsExpired)
            reasons.Add($"Thẻ đã hết hạn ngày {reader.ExpiryDate.ToVnDateText()}.");
        return reasons;
    }

    // ------------------------------------------------------------------
    // UC07 Lập phiếu mượn
    // ------------------------------------------------------------------

    // UC07 bước 2–3: tìm độc giả theo mã thẻ và kiểm tra điều kiện mượn (QĐ02, QĐ07).
    public async Task<ServiceResult<BorrowerCheckVM>> CheckBorrowerAsync(string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return ServiceResult<BorrowerCheckVM>.Fail("Vui lòng nhập mã thẻ độc giả.", nameof(CreateLoanVM.CardNumber));

        var code = cardNumber.Trim().ToUpperInvariant();
        var reader = await _db.Readers.Find(r => r.CardNumber == code).FirstOrDefaultAsync();
        if (reader == null)   // bước 2a
            return ServiceResult<BorrowerCheckVM>.Fail($"Không tìm thấy độc giả có mã thẻ {code}.", nameof(CreateLoanVM.CardNumber));

        var overdue = (int)await _db.Loans.CountDocumentsAsync(OverdueFilter(reader.Id!));
        var borrowing = await CountBorrowingBooksAsync(reader.Id!);
        await _reservations.Value.ExpireHoldsAsync();   // trả về kho các bản giữ đã quá hạn nhận trước khi đọc
        var heldBookIds = await _reservations.Value.GetHeldBookIdsAsync(reader.Id!);

        var reasons = CardProblems(reader);   // bước 3a
        if (overdue > 0)
            reasons.Add($"Độc giả đang có {overdue} phiếu quá hạn, cần trả sách trước khi mượn tiếp.");
        if (borrowing >= _rules.MaxBooksPerReader)
            reasons.Add($"Độc giả đã mượn đủ {_rules.MaxBooksPerReader} cuốn, cần trả bớt trước khi mượn tiếp.");

        return ServiceResult<BorrowerCheckVM>.Ok(new BorrowerCheckVM
        {
            Reader = reader,
            BorrowingBooks = borrowing,
            OverdueLoans = overdue,
            MaxBooks = _rules.MaxBooksPerReader,
            Reasons = reasons,
            HeldBookIds = heldBookIds
        });
    }

    // UC07 bước 1 (tab "Danh sách độc giả"): mọi độc giả kèm số đang mượn, sách đang giữ và tình trạng.
    // 3 truy vấn gộp (đang mượn, quá hạn, đặt giữ) thay vì kiểm tra từng độc giả.
    public async Task<List<ReaderBoardRowVM>> GetReaderBoardAsync()
    {
        await _reservations.Value.ExpireHoldsAsync();   // trả về kho các bản giữ quá hạn trước khi đếm
        var readers = await _db.Readers.Find(_ => true).ToListAsync();

        // Số cuốn chưa trả theo độc giả: $match → $unwind items → $match chưa trả → $group
        var borrowing = (await _db.Loans.Aggregate<BsonDocument>(new[]
        {
            new BsonDocument("$match", new BsonDocument("status", LoanStatus.Borrowing.ToString())),
            new BsonDocument("$unwind", "$items"),
            new BsonDocument("$match", new BsonDocument("items.returnDate", BsonNull.Value)),
            new BsonDocument("$group", new BsonDocument { { "_id", "$reader.readerId" }, { "n", new BsonDocument("$sum", 1) } })
        }).ToListAsync()).ToDictionary(d => d["_id"].ToString()!, d => d["n"].ToInt32());

        // Độc giả có phiếu quá hạn
        var overdue = (await _db.Loans.Find(l => l.Status == LoanStatus.Borrowing && l.DueDate < DateHelper.StartOfTodayVnUtc())
            .Project(l => l.Reader.ReaderId).ToListAsync()).ToHashSet();

        // Sách đang giữ: số cuốn và hạn nhận sớm nhất theo độc giả
        var holds = (await _db.Reservations.Find(r => r.Status == ReservationStatus.Held).ToListAsync())
            .GroupBy(r => r.ReaderId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Until: g.Min(r => r.HoldUntil)));

        return readers.Select(r =>
            {
                var n = borrowing.GetValueOrDefault(r.Id!);
                var hold = holds.GetValueOrDefault(r.Id!);
                var state = r.CardStatus == CardStatus.Locked ? BoardReaderState.Locked
                    : r.IsExpired ? BoardReaderState.Expired
                    : overdue.Contains(r.Id!) ? BoardReaderState.Overdue
                    : n >= _rules.MaxBooksPerReader ? BoardReaderState.Full
                    : BoardReaderState.Active;
                return new ReaderBoardRowVM
                {
                    CardNumber = r.CardNumber,
                    FullName = r.FullName,
                    Phone = r.Phone,
                    BorrowingBooks = n,
                    MaxBooks = _rules.MaxBooksPerReader,
                    HeldCount = hold.Count,
                    EarliestHoldUntil = hold.Until,
                    State = state
                };
            })
            .OrderBy(x => x.Rank)
            .ThenByDescending(x => x.Rank == 0 ? x.HeldCount : 0)
            .ThenBy(x => x.CardNumber)
            .ToList();
    }

    // UC07 (tab "Phiếu mượn gần đây"): các phiếu mới lập nhất.
    public async Task<List<RecentLoanVM>> GetRecentLoansAsync(int limit = 50)
    {
        var loans = await _db.Loans.Find(_ => true)
            .SortByDescending(l => l.BorrowDate).ThenByDescending(l => l.LoanCode)
            .Limit(limit)
            .ToListAsync();

        return loans.Select(l => new RecentLoanVM
        {
            Id = l.Id!,
            LoanCode = l.LoanCode,
            CardNumber = l.Reader.CardNumber,
            ReaderName = l.Reader.FullName,
            Titles = l.Items.Select(i => i.Title).ToList(),
            BorrowDate = l.BorrowDate,
            DueDate = l.DueDate,
            Status = l.Status == LoanStatus.Returned ? "returned" : l.IsOverdue ? "overdue" : "borrowing"
        }).ToList();
    }

    // UC07 bước 4: danh sách đầu sách để chọn (kèm số còn lại).
    public async Task<List<BookOptionVM>> GetBookOptionsAsync()
    {
        var books = await _db.Books.Find(_ => true).SortBy(b => b.Title).ToListAsync();
        return books.Select(b => new BookOptionVM
        {
            Id = b.Id!,
            Title = b.Title,
            Authors = string.Join(", ", b.Authors),
            AvailableQuantity = b.AvailableQuantity,
            TotalQuantity = b.TotalQuantity
        }).ToList();
    }

    // UC07 bước 5–8: lập phiếu. Kiểm tra lại toàn bộ điều kiện ở server, rồi trong MỘT transaction:
    // trừ availableQuantity từng sách bằng update có điều kiện "> 0" → tạo phiếu → hoàn tất đặt trước (bước 5c).
    public async Task<ServiceResult<Loan>> CreateAsync(string? cardNumber, List<string> bookIds, DateTime? dueDate, string librarianId)
    {
        var check = await CheckBorrowerAsync(cardNumber);
        if (!check.Success) return ServiceResult<Loan>.Fail(check.Message!, check.Field);
        var borrower = check.Data!;
        var reader = borrower.Reader;
        if (!borrower.CanBorrow)   // bước 3a
            return ServiceResult<Loan>.Fail("Độc giả không được mượn: " + string.Join(" ", borrower.Reasons));

        var ids = bookIds.Where(ValidId).Distinct().ToList();
        if (ids.Count == 0)
            return ServiceResult<Loan>.Fail("Vui lòng chọn ít nhất một sách.", nameof(CreateLoanVM.BookIds));

        // Bước 5b (QĐ02). Sách thư viện đang giữ cho độc giả mà lần này KHÔNG mượn vẫn chiếm kho,
        // nên tính vào giới hạn: tránh bỏ tick sách đang giữ rồi chọn sách khác để vượt tổng MaxBooksPerReader.
        var unpickedHeld = borrower.HeldBookIds.Count(h => !ids.Contains(h));
        if (borrower.BorrowingBooks + ids.Count + unpickedHeld > _rules.MaxBooksPerReader)
            return ServiceResult<Loan>.Fail(unpickedHeld == 0
                ? $"Độc giả đang mượn {borrower.BorrowingBooks} cuốn, chỉ còn được mượn thêm {borrower.Remaining} cuốn (tối đa {_rules.MaxBooksPerReader})."
                : $"Độc giả đang mượn {borrower.BorrowingBooks} cuốn và thư viện đang giữ {unpickedHeld} cuốn khác chưa nhận, " +
                  $"tổng không quá {_rules.MaxBooksPerReader} cuốn. Hãy bỏ bớt sách, chọn sách đang giữ hoặc hủy đặt giữ trước.",
                nameof(CreateLoanVM.BookIds));

        // Bước 7a
        var today = DateHelper.TodayVn();
        if (dueDate == null)
            return ServiceResult<Loan>.Fail("Vui lòng chọn hạn trả.", nameof(CreateLoanVM.DueDate));
        if (dueDate.Value.Date <= today)
            return ServiceResult<Loan>.Fail("Hạn trả phải sau ngày mượn (hôm nay).", nameof(CreateLoanVM.DueDate));

        var books = await _db.Books.Find(b => ids.Contains(b.Id!)).ToListAsync();
        if (books.Count != ids.Count)
            return ServiceResult<Loan>.Fail("Có sách không còn trong danh mục, vui lòng chọn lại.", nameof(CreateLoanVM.BookIds));

        // Sách thư viện đang giữ cho độc giả này: bản sách đã trừ kho từ lúc đặt giữ
        var heldSet = borrower.HeldBookIds.ToHashSet();

        // Bước 5a: báo trước những sách đã hết (transaction bên dưới vẫn chặn trường hợp vừa hết đúng lúc)
        var outOfStock = books.Where(b => b.AvailableQuantity <= 0 && !heldSet.Contains(b.Id!)).ToList();
        if (outOfStock.Count > 0)
            return ServiceResult<Loan>.Fail(
                $"Sách đã hết: {string.Join(", ", outOfStock.Select(b => $"\"{b.Title}\""))}. Đã bỏ khỏi danh sách, vui lòng chọn lại.",
                nameof(CreateLoanVM.BookIds));

        var loan = new Loan
        {
            LoanCode = await _counters.NextLoanCodeAsync(),
            Reader = new LoanReader { ReaderId = reader.Id!, CardNumber = reader.CardNumber, FullName = reader.FullName },
            LibrarianId = librarianId,
            BorrowDate = DateHelper.VnDateToUtc(today),
            DueDate = DateHelper.VnDateToUtc(dueDate.Value),
            Status = LoanStatus.Borrowing,
            Items = ids.Select(id => books.First(b => b.Id == id)).Select(b => new LoanItem
            {
                BookId = b.Id!,
                Title = b.Title,
                CategoryIds = b.CategoryIds
            }).ToList()
        };

        try
        {
            using var session = await _db.Client.StartSessionAsync();
            await session.WithTransactionAsync(async (s, ct) =>
            {
                loan.Id = ObjectId.GenerateNewId().ToString();   // cần Id trước để ghi vào yêu cầu đặt giữ; thử lại thì tạo Id mới
                foreach (var item in loan.Items)
                {
                    // Sách đang giữ cho độc giả: chuyển yêu cầu Held → Completed, không trừ kho lần nữa
                    if (heldSet.Contains(item.BookId)
                        && await _reservations.Value.ConsumeHoldAsync(s, reader.Id!, item.BookId, loan.Id))
                        continue;

                    // Mục 7.2: gộp điều kiện vào filter, không đọc rồi mới ghi
                    var dec = await _db.Books.UpdateOneAsync(s,
                        b => b.Id == item.BookId && b.AvailableQuantity > 0,
                        Builders<Book>.Update.Inc(b => b.AvailableQuantity, -1),
                        cancellationToken: ct);
                    if (dec.ModifiedCount == 0)
                        throw new BusinessException($"Sách \"{item.Title}\" vừa hết, vui lòng bỏ khỏi danh sách.");
                }

                await _db.Loans.InsertOneAsync(s, loan, cancellationToken: ct);

                // Bước 5c: yêu cầu đặt trước Pending / Approved của độc giả cho các sách này → Completed
                await _reservations.Value.CompleteForLoanAsync(s, reader.Id!, ids, loan.Id!);
                return true;
            });
        }
        catch (BusinessException ex)
        {
            return ServiceResult<Loan>.Fail(ex.Message, nameof(CreateLoanVM.BookIds));
        }

        return ServiceResult<Loan>.Ok(loan,
            $"Đã lập phiếu {loan.LoanCode} cho {reader.FullName}: {loan.Items.Count} cuốn, hạn trả {loan.DueDate.ToVnDateText()}.");
    }

    // ------------------------------------------------------------------
    // UC08 Nhận trả sách
    // ------------------------------------------------------------------

    // UC08 bước 2–3: tìm theo mã phiếu (PM...) hoặc mã thẻ (DG...), lấy các phiếu đang mượn và sách chưa trả.
    public async Task<ReturnSearchVM> FindForReturnAsync(string? keyword)
    {
        var vm = new ReturnSearchVM { Keyword = keyword, FinePerDay = _rules.FinePerDay };
        if (string.IsNullOrWhiteSpace(keyword)) return vm;

        var code = keyword.Trim().ToUpperInvariant();
        List<Loan> loans;
        if (code.StartsWith("PM"))
        {
            var loan = await _db.Loans.Find(l => l.LoanCode == code).FirstOrDefaultAsync();
            if (loan == null) { vm.Notice = $"Không tìm thấy phiếu mượn {code}."; return vm; }
            if (loan.Status == LoanStatus.Returned) { vm.Notice = $"Phiếu {code} đã trả hết sách, không có sách cần trả."; return vm; }
            loans = new() { loan };
        }
        else
        {
            var reader = await _db.Readers.Find(r => r.CardNumber == code).FirstOrDefaultAsync();
            if (reader == null) { vm.Notice = $"Không tìm thấy độc giả có mã thẻ {code}."; return vm; }
            loans = await _db.Loans.Find(l => l.Reader.ReaderId == reader.Id && l.Status == LoanStatus.Borrowing)
                .SortBy(l => l.DueDate).ToListAsync();
            if (loans.Count == 0)   // bước 3a
            {
                vm.Notice = $"Độc giả {reader.FullName} ({reader.CardNumber}) không có phiếu nào đang mượn, không có sách cần trả.";
                return vm;
            }
        }

        var bookIds = loans.SelectMany(l => l.Items).Select(i => i.BookId).Distinct().ToList();
        var prices = (await _db.Books.Find(b => bookIds.Contains(b.Id!)).ToListAsync()).ToDictionary(b => b.Id!, b => b.Price);

        vm.Loans = loans.Select(l => new ReturnLoanVM
        {
            LoanId = l.Id!,
            LoanCode = l.LoanCode,
            CardNumber = l.Reader.CardNumber,
            ReaderName = l.Reader.FullName,
            BorrowDate = l.BorrowDate,
            DueDate = l.DueDate,
            OverdueDays = l.OverdueDays,
            TotalItems = l.Items.Count,
            Items = l.Items.Where(i => i.ReturnDate == null).Select(i => new ReturnItemVM
            {
                BookId = i.BookId,
                Title = i.Title,
                Price = prices.GetValueOrDefault(i.BookId),
                OverdueFine = l.OverdueDays * _rules.FinePerDay
            }).ToList()
        }).ToList();
        return vm;
    }

    // UC08 bước 4–8: ghi nhận trả, tính phạt, cập nhật kho, đóng phiếu nếu đã trả hết; sau đó lấy danh sách người chờ (7a).
    public async Task<ServiceResult<ReturnResultVM>> ReturnAsync(ReturnLoanInput input)
    {
        var loan = await GetByIdAsync(input.LoanId);
        if (loan == null) return ServiceResult<ReturnResultVM>.Fail("Không tìm thấy phiếu mượn.");
        if (loan.Status == LoanStatus.Returned) return ServiceResult<ReturnResultVM>.Fail($"Phiếu {loan.LoanCode} đã trả hết sách.");

        var selected = input.Items.Where(i => i.Selected).GroupBy(i => i.BookId).Select(g => g.First()).ToList();
        if (selected.Count == 0)
            return ServiceResult<ReturnResultVM>.Fail("Vui lòng chọn ít nhất một cuốn được trả.");

        var bookIds = selected.Select(i => i.BookId).ToList();
        var books = (await _db.Books.Find(b => bookIds.Contains(b.Id!)).ToListAsync()).ToDictionary(b => b.Id!);

        // Bước 5: tính phạt từng cuốn. Quá hạn: số ngày trễ × FinePerDay (QĐ05). Hư hỏng: mức Thủ thư nhập. Mất: giá sách (QĐ06).
        var overdueFine = loan.OverdueDays * _rules.FinePerDay;
        var lines = new List<(LoanItem Item, ReturnItemInput In, long OverdueFine, long Fine)>();
        foreach (var sel in selected)
        {
            var item = loan.Items.FirstOrDefault(i => i.BookId == sel.BookId);
            if (item == null) return ServiceResult<ReturnResultVM>.Fail("Có sách không thuộc phiếu này.");
            if (item.ReturnDate != null) return ServiceResult<ReturnResultVM>.Fail($"Cuốn \"{item.Title}\" đã được trả trước đó.");

            long fine = overdueFine;
            switch (sel.Condition)
            {
                case BookCondition.Damaged:
                    if (sel.DamageFine is null or <= 0)
                        return ServiceResult<ReturnResultVM>.Fail($"Vui lòng nhập mức phạt hư hỏng cho cuốn \"{item.Title}\".");
                    fine += sel.DamageFine.Value;
                    break;
                case BookCondition.Lost:
                    if (!books.TryGetValue(item.BookId, out var lostBook))
                        return ServiceResult<ReturnResultVM>.Fail($"Không tìm thấy giá của cuốn \"{item.Title}\" để tính bồi thường.");
                    fine += lostBook.Price;
                    break;
            }
            lines.Add((item, sel, overdueFine, fine));
        }

        var now = DateTime.UtcNow;
        bool closed = false;
        try
        {
            using var session = await _db.Client.StartSessionAsync();
            await session.WithTransactionAsync(async (s, ct) =>
            {
                foreach (var (item, sel, _, fine) in lines)
                {
                    // Cập nhật đúng phần tử trong mảng items bằng arrayFilters: cuốn này và còn chưa trả
                    var update = Builders<Loan>.Update
                        .Set(l => l.Items.AllMatchingElements("it").ReturnDate, (DateTime?)now)
                        .Set(l => l.Items.AllMatchingElements("it").Condition, (BookCondition?)sel.Condition)
                        .Set(l => l.Items.AllMatchingElements("it").Fine, fine)
                        .Inc(l => l.TotalFine, fine);
                    var options = new UpdateOptions
                    {
                        ArrayFilters = new[]
                        {
                            new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument
                            {
                                { "it.bookId", ObjectId.Parse(item.BookId) },
                                { "it.returnDate", BsonNull.Value }
                            })
                        }
                    };
                    var r = await _db.Loans.UpdateOneAsync(s,
                        Builders<Loan>.Filter.Eq(l => l.Id, loan.Id)
                        & Builders<Loan>.Filter.Eq(l => l.Status, LoanStatus.Borrowing)
                        & Builders<Loan>.Filter.ElemMatch(l => l.Items, i => i.BookId == item.BookId && i.ReturnDate == null),
                        update, options, ct);
                    if (r.ModifiedCount == 0)
                        throw new BusinessException($"Cuốn \"{item.Title}\" vừa được trả ở nơi khác, vui lòng tải lại.");

                    // Bước 7: Bình thường / Hư hỏng cộng lại vào kho; Mất thì trừ khỏi số lượng tổng
                    var bookUpdate = sel.Condition == BookCondition.Lost
                        ? Builders<Book>.Update.Inc(b => b.TotalQuantity, -1)
                        : Builders<Book>.Update.Inc(b => b.AvailableQuantity, 1);
                    await _db.Books.UpdateOneAsync(s, b => b.Id == item.BookId, bookUpdate, cancellationToken: ct);
                }

                // Bước 8 / 8a: chỉ chuyển Returned khi không còn cuốn nào chưa trả
                var close = await _db.Loans.UpdateOneAsync(s,
                    Builders<Loan>.Filter.Eq(l => l.Id, loan.Id)
                    & Builders<Loan>.Filter.Not(Builders<Loan>.Filter.ElemMatch(l => l.Items, i => i.ReturnDate == null)),
                    Builders<Loan>.Update.Set(l => l.Status, LoanStatus.Returned),
                    cancellationToken: ct);
                closed = close.ModifiedCount == 1;
                return true;
            });
        }
        catch (BusinessException ex)
        {
            return ServiceResult<ReturnResultVM>.Fail(ex.Message);
        }

        // Bước 7a (sau transaction): ai đang chờ các sách vừa về kho (không tính sách mất)
        var backInStock = lines.Where(l => l.In.Condition != BookCondition.Lost).Select(l => l.Item.BookId).Distinct().ToList();
        var waiting = await _reservations.Value.GetWaitingListAsync(backInStock);

        var result = new ReturnResultVM
        {
            LoanId = loan.Id!,
            LoanCode = loan.LoanCode,
            ReaderName = loan.Reader.FullName,
            CardNumber = loan.Reader.CardNumber,
            Lines = lines.Select(l => new ReturnedLine
            {
                Title = l.Item.Title,
                Condition = l.In.Condition,
                OverdueFine = l.OverdueFine,
                Fine = l.Fine
            }).ToList(),
            LoanClosed = closed,
            RemainingItems = loan.Items.Count(i => i.ReturnDate == null) - lines.Count,
            Waiting = waiting
        };
        var msg = closed
            ? $"Đã nhận trả {lines.Count} cuốn. Phiếu {loan.LoanCode} đã trả hết."
            : $"Đã nhận trả {lines.Count} cuốn. Phiếu {loan.LoanCode} còn {result.RemainingItems} cuốn chưa trả.";
        return ServiceResult<ReturnResultVM>.Ok(result, msg);
    }

    // ------------------------------------------------------------------
    // UC14 Sách đang mượn và lịch sử (ReaderId lấy từ claim, controller không nhận từ form)
    // ------------------------------------------------------------------

    // UC14 bước 2: các phiếu đang mượn của độc giả, hạn trả gần nhất lên đầu.
    public Task<List<Loan>> GetCurrentLoansAsync(string readerId) =>
        _db.Loans.Find(l => l.Reader.ReaderId == readerId && l.Status == LoanStatus.Borrowing)
            .SortBy(l => l.DueDate).ToListAsync();

    // UC14 bước 4: các cuốn đã trả (kể cả cuốn đã trả trong phiếu còn đang mượn), trả gần nhất lên đầu.
    public async Task<List<HistoryLineVM>> GetHistoryAsync(string readerId)
    {
        var loans = await _db.Loans
            .Find(Builders<Loan>.Filter.Eq(l => l.Reader.ReaderId, readerId)
                  & Builders<Loan>.Filter.ElemMatch(l => l.Items, i => i.ReturnDate != null))
            .ToListAsync();

        return loans
            .SelectMany(l => l.Items.Where(i => i.ReturnDate != null).Select(i => new HistoryLineVM
            {
                LoanCode = l.LoanCode,
                Title = i.Title,
                BorrowDate = l.BorrowDate,
                ReturnDate = i.ReturnDate!.Value,
                Condition = i.Condition,
                Fine = i.Fine
            }))
            .OrderByDescending(x => x.ReturnDate)
            .ToList();
    }

    // ------------------------------------------------------------------
    // Gia hạn: điều kiện dùng chung cho UC15 và UC09 (QĐ04)
    // ------------------------------------------------------------------

    private DateTime NewDueDate(Loan loan) =>
        DateHelper.VnDateToUtc(DateHelper.ToVnDate(loan.DueDate).AddDays(_rules.RenewalDays));

    private static RenewalRequest? PendingRequest(Loan loan) =>
        loan.RenewalRequests.FirstOrDefault(r => r.Status == RenewalStatus.Pending);

    /// <summary>
    /// Lý do phiếu không được gia hạn. checkWaiting = true thì xét thêm "có người đặt trước sách trong phiếu"
    /// (UC09 bước 4); UC15 bước 2 không xét điều này, Thủ thư sẽ xét khi duyệt.
    /// </summary>
    private async Task<List<string>> RenewalProblemsAsync(Loan loan, Reader? reader, bool checkWaiting)
    {
        var problems = new List<string>();
        if (loan.Status != LoanStatus.Borrowing)
        {
            problems.Add("Phiếu đã trả hết sách.");
            return problems;
        }
        if (reader == null) problems.Add("Không tìm thấy hồ sơ độc giả.");
        else problems.AddRange(CardProblems(reader));
        if (loan.IsOverdue)
            problems.Add($"Phiếu đã quá hạn {loan.OverdueDays} ngày (hạn trả {loan.DueDate.ToVnDateText()}).");
        if (loan.RenewalCount >= _rules.MaxRenewals)
            problems.Add($"Phiếu đã gia hạn đủ {_rules.MaxRenewals} lần.");

        if (checkWaiting)
        {
            var openBookIds = loan.Items.Where(i => i.ReturnDate == null).Select(i => i.BookId).ToList();
            if (await _reservations.Value.HasWaitingAsync(openBookIds))
                problems.Add("Có độc giả khác đang đặt trước sách trong phiếu.");
        }
        return problems;
    }

    private async Task<RenewalVM> BuildRenewalAsync(Loan loan, Reader? reader, bool checkWaiting) => new()
    {
        LoanId = loan.Id!,
        LoanCode = loan.LoanCode,
        CardNumber = loan.Reader.CardNumber,
        ReaderName = loan.Reader.FullName,
        Titles = loan.Items.Where(i => i.ReturnDate == null).Select(i => i.Title).ToList(),
        DueDate = loan.DueDate,
        NewDueDate = NewDueDate(loan),
        RenewalCount = loan.RenewalCount,
        MaxRenewals = _rules.MaxRenewals,
        RenewalDays = _rules.RenewalDays,
        PendingRequestedAt = PendingRequest(loan)?.RequestedAt,
        Problems = await RenewalProblemsAsync(loan, reader, checkWaiting)
    };

    // ------------------------------------------------------------------
    // UC15 Yêu cầu gia hạn (Độc giả)
    // ------------------------------------------------------------------

    // UC15 bước 2–3: kiểm tra và hiện hạn trả dự kiến. Phiếu phải thuộc độc giả đang đăng nhập.
    public async Task<ServiceResult<RenewalVM>> GetRenewalPreviewAsync(string readerId, string loanId)
    {
        var loan = await GetByIdAsync(loanId);
        if (loan == null || loan.Reader.ReaderId != readerId)
            return ServiceResult<RenewalVM>.Fail("Không tìm thấy phiếu mượn của bạn.");

        var reader = await _db.Readers.Find(r => r.Id == readerId).FirstOrDefaultAsync();
        var vm = await BuildRenewalAsync(loan, reader, checkWaiting: false);
        if (vm.PendingRequestedAt != null)
            vm.Problems.Add("Phiếu đã có yêu cầu gia hạn đang chờ duyệt.");
        return ServiceResult<RenewalVM>.Ok(vm);
    }

    // UC15 bước 4–5: lưu yêu cầu Pending bằng $push. Filter bảo đảm phiếu chưa có phần tử Pending,
    // nên gửi hai lần liên tiếp (hoặc hai tab) cũng chỉ tạo một yêu cầu.
    public async Task<ServiceResult> RequestRenewalAsync(string readerId, string loanId)
    {
        var preview = await GetRenewalPreviewAsync(readerId, loanId);
        if (!preview.Success) return preview;
        if (!preview.Data!.Eligible)   // bước 2a
            return ServiceResult.Fail("Không thể gửi yêu cầu gia hạn: " + string.Join(" ", preview.Data.Problems));

        var f = Builders<Loan>.Filter;
        var filter = f.Eq(l => l.Id, loanId)
            & f.Eq(l => l.Reader.ReaderId, readerId)
            & f.Eq(l => l.Status, LoanStatus.Borrowing)
            & f.Lt(l => l.RenewalCount, _rules.MaxRenewals)
            & f.Not(f.ElemMatch(l => l.RenewalRequests, r => r.Status == RenewalStatus.Pending));
        var update = Builders<Loan>.Update.Push(l => l.RenewalRequests, new RenewalRequest
        {
            RequestedAt = DateTime.UtcNow,
            Source = RenewalSource.Online,
            Status = RenewalStatus.Pending
        });

        var result = await _db.Loans.UpdateOneAsync(filter, update);
        return result.ModifiedCount == 1
            ? ServiceResult.Ok($"Đã gửi yêu cầu gia hạn phiếu {preview.Data.LoanCode}. Thủ thư sẽ xem xét và duyệt.")
            : ServiceResult.Fail("Phiếu đã có yêu cầu gia hạn đang chờ duyệt.");
    }

    // ------------------------------------------------------------------
    // UC09 Gia hạn phiếu mượn (Thủ thư)
    // ------------------------------------------------------------------

    // UC09 bước 2, 4: các phiếu có yêu cầu Pending, kèm kết quả kiểm tra điều kiện.
    public async Task<List<RenewalVM>> GetPendingRenewalsAsync()
    {
        var loans = await _db.Loans
            .Find(Builders<Loan>.Filter.ElemMatch(l => l.RenewalRequests, r => r.Status == RenewalStatus.Pending))
            .ToListAsync();

        var readerIds = loans.Select(l => l.Reader.ReaderId).Distinct().ToList();
        var readers = (await _db.Readers.Find(r => readerIds.Contains(r.Id!)).ToListAsync()).ToDictionary(r => r.Id!);

        var list = new List<RenewalVM>();
        foreach (var loan in loans)
            list.Add(await BuildRenewalAsync(loan, readers.GetValueOrDefault(loan.Reader.ReaderId), checkWaiting: true));
        return list.OrderBy(x => x.PendingRequestedAt).ToList();
    }

    // UC09 bước 1a: Thủ thư nhập mã phiếu để gia hạn tại quầy.
    public async Task<ServiceResult<RenewalVM>> GetCounterRenewalAsync(string loanCode)
    {
        var code = loanCode.Trim().ToUpperInvariant();
        var loan = await _db.Loans.Find(l => l.LoanCode == code).FirstOrDefaultAsync();
        if (loan == null) return ServiceResult<RenewalVM>.Fail($"Không tìm thấy phiếu mượn {code}.");

        var reader = await _db.Readers.Find(r => r.Id == loan.Reader.ReaderId).FirstOrDefaultAsync();
        return ServiceResult<RenewalVM>.Ok(await BuildRenewalAsync(loan, reader, checkWaiting: true));
    }

    // UC09 bước 4–6: duyệt yêu cầu Pending. Kiểm tra lại điều kiện ở server (4a: không đủ thì không duyệt được).
    // $inc renewalCount, cộng RenewalDays vào dueDate, cập nhật đúng phần tử Pending bằng positional "$".
    public async Task<ServiceResult> ApproveAsync(string loanId, string librarianId)
    {
        var loan = await GetByIdAsync(loanId);
        if (loan == null) return ServiceResult.Fail("Không tìm thấy phiếu mượn.");
        if (PendingRequest(loan) == null) return ServiceResult.Fail($"Phiếu {loan.LoanCode} không có yêu cầu gia hạn đang chờ duyệt.");

        var check = await CheckForRenewAsync(loan);
        if (check != null) return check;

        var f = Builders<Loan>.Filter;
        var newDue = NewDueDate(loan);
        var result = await _db.Loans.UpdateOneAsync(
            SameStateFilter(loan) & f.ElemMatch(l => l.RenewalRequests, r => r.Status == RenewalStatus.Pending),
            Builders<Loan>.Update
                .Inc(l => l.RenewalCount, 1)
                .Set(l => l.DueDate, newDue)
                .Set(l => l.RenewalRequests.FirstMatchingElement().Status, RenewalStatus.Approved)
                .Set(l => l.RenewalRequests.FirstMatchingElement().ProcessedAt, (DateTime?)DateTime.UtcNow)
                .Set(l => l.RenewalRequests.FirstMatchingElement().ProcessedBy, librarianId));

        return result.ModifiedCount == 1
            ? ServiceResult.Ok($"Đã duyệt gia hạn phiếu {loan.LoanCode}, hạn trả mới {newDue.ToVnDateText()}.")
            : ServiceResult.Fail("Phiếu vừa được cập nhật ở nơi khác, vui lòng tải lại.");
    }

    // UC09 bước 5a: từ chối kèm lý do, hạn trả giữ nguyên.
    public async Task<ServiceResult> RejectAsync(string loanId, string librarianId, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return ServiceResult.Fail("Vui lòng nhập lý do từ chối.");

        var loan = await GetByIdAsync(loanId);
        if (loan == null) return ServiceResult.Fail("Không tìm thấy phiếu mượn.");

        var result = await _db.Loans.UpdateOneAsync(
            Builders<Loan>.Filter.Eq(l => l.Id, loanId)
            & Builders<Loan>.Filter.ElemMatch(l => l.RenewalRequests, r => r.Status == RenewalStatus.Pending),
            Builders<Loan>.Update
                .Set(l => l.RenewalRequests.FirstMatchingElement().Status, RenewalStatus.Rejected)
                .Set(l => l.RenewalRequests.FirstMatchingElement().RejectReason, reason.Trim())
                .Set(l => l.RenewalRequests.FirstMatchingElement().ProcessedAt, (DateTime?)DateTime.UtcNow)
                .Set(l => l.RenewalRequests.FirstMatchingElement().ProcessedBy, librarianId));

        return result.ModifiedCount == 1
            ? ServiceResult.Ok($"Đã từ chối yêu cầu gia hạn phiếu {loan.LoanCode}.")
            : ServiceResult.Fail($"Phiếu {loan.LoanCode} không có yêu cầu gia hạn đang chờ duyệt.");
    }

    // UC09 bước 1a: gia hạn tại quầy. Nếu phiếu đang có yêu cầu online Pending thì duyệt luôn yêu cầu đó;
    // nếu không thì thêm một yêu cầu nguồn Counter ở trạng thái Approved, cùng lệnh update với việc cộng hạn trả.
    public async Task<ServiceResult> RenewAtCounterAsync(string loanId, string librarianId)
    {
        var loan = await GetByIdAsync(loanId);
        if (loan == null) return ServiceResult.Fail("Không tìm thấy phiếu mượn.");
        if (PendingRequest(loan) != null) return await ApproveAsync(loanId, librarianId);

        var check = await CheckForRenewAsync(loan);
        if (check != null) return check;

        var f = Builders<Loan>.Filter;
        var now = DateTime.UtcNow;
        var newDue = NewDueDate(loan);
        var result = await _db.Loans.UpdateOneAsync(
            SameStateFilter(loan) & f.Not(f.ElemMatch(l => l.RenewalRequests, r => r.Status == RenewalStatus.Pending)),
            Builders<Loan>.Update
                .Inc(l => l.RenewalCount, 1)
                .Set(l => l.DueDate, newDue)
                .Push(l => l.RenewalRequests, new RenewalRequest
                {
                    RequestedAt = now,
                    Source = RenewalSource.Counter,
                    Status = RenewalStatus.Approved,
                    ProcessedAt = now,
                    ProcessedBy = librarianId
                }));

        return result.ModifiedCount == 1
            ? ServiceResult.Ok($"Đã gia hạn tại quầy phiếu {loan.LoanCode}, hạn trả mới {newDue.ToVnDateText()}.")
            : ServiceResult.Fail("Phiếu vừa được cập nhật ở nơi khác, vui lòng tải lại.");
    }

    /// <summary>Kiểm tra đủ QĐ04 (kể cả có người đặt trước). Trả null nếu được gia hạn.</summary>
    private async Task<ServiceResult?> CheckForRenewAsync(Loan loan)
    {
        var reader = await _db.Readers.Find(r => r.Id == loan.Reader.ReaderId).FirstOrDefaultAsync();
        var problems = await RenewalProblemsAsync(loan, reader, checkWaiting: true);
        return problems.Count == 0
            ? null
            : ServiceResult.Fail($"Phiếu {loan.LoanCode} không đủ điều kiện gia hạn: " + string.Join(" ", problems));
    }

    /// <summary>Phiếu vẫn ở đúng trạng thái đã đọc (chống hai lần duyệt cùng lúc cộng hạn hai lần).</summary>
    private FilterDefinition<Loan> SameStateFilter(Loan loan)
    {
        var f = Builders<Loan>.Filter;
        return f.Eq(l => l.Id, loan.Id)
            & f.Eq(l => l.Status, LoanStatus.Borrowing)
            & f.Eq(l => l.RenewalCount, loan.RenewalCount)
            & f.Lt(l => l.RenewalCount, _rules.MaxRenewals)
            & f.Eq(l => l.DueDate, loan.DueDate);
    }

    // ------------------------------------------------------------------
    // Hàm giao kèo cho B (phan-cong.md mục 3). Không đổi chữ ký.
    // ------------------------------------------------------------------

    /// <summary>Độc giả có đang mượn cuốn sách này (phiếu Borrowing, item chưa trả) không. B gọi trong UC16.</summary>
    public Task<bool> IsBorrowingAsync(string readerId, string bookId)
    {
        var f = Builders<Loan>.Filter;
        var filter = f.Eq(l => l.Reader.ReaderId, readerId)
            & f.Eq(l => l.Status, LoanStatus.Borrowing)
            & f.ElemMatch(l => l.Items, i => i.BookId == bookId && i.ReturnDate == null);
        return _db.Loans.Find(filter).AnyAsync();
    }

    /// <summary>Sách có nằm trong phiếu mượn đang mở (Borrowing, cuốn đó chưa trả) nào không. B gọi khi xóa sách ở UC04.</summary>
    public Task<bool> HasOpenLoanForBookAsync(string bookId)
    {
        var f = Builders<Loan>.Filter;
        var filter = f.Eq(l => l.Status, LoanStatus.Borrowing)
            & f.ElemMatch(l => l.Items, i => i.BookId == bookId && i.ReturnDate == null);
        return _db.Loans.Find(filter).AnyAsync();
    }
}
