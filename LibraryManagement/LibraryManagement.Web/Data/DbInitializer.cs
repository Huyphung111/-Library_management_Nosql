using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Options;
using LibraryManagement.Web.Services;

namespace LibraryManagement.Web.Data;

/// <summary>
/// Khởi tạo Index và Seed dữ liệu mẫu cho hệ thống quản lý mượn trả sách (Member B).
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

        await CreateIndexesAsync(db);
        var counterService = scope.ServiceProvider.GetRequiredService<CounterService>();
        var rules = scope.ServiceProvider.GetRequiredService<IOptions<LibraryRules>>().Value;

        await SeedAsync(db, counterService, rules);
        await EnsureRichLoansAsync(db, counterService, rules);

        var env = scope.ServiceProvider.GetService<IWebHostEnvironment>();
        if (env != null)
        {
            await ExportMongoDataAsync(db, env);
        }
    }

    // ---------------------------------------------------------------- Index (mục 5.4)

    private static async Task CreateIndexesAsync(MongoDbContext db)
    {
        // users
        await db.Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Username),
            new CreateIndexOptions { Unique = true, Name = "ux_username" }));

        // readers
        await db.Readers.Indexes.CreateOneAsync(new CreateIndexModel<Reader>(
            Builders<Reader>.IndexKeys.Ascending(r => r.CardNumber),
            new CreateIndexOptions { Unique = true, Name = "ux_cardNumber" }));
        await db.Readers.Indexes.CreateOneAsync(new CreateIndexModel<Reader>(
            Builders<Reader>.IndexKeys.Ascending(r => r.IdNumber),
            new CreateIndexOptions { Unique = true, Name = "ux_idNumber" }));

        // categories: tên duy nhất, không phân biệt hoa thường (collation vi, strength 2)
        await db.Categories.Indexes.CreateOneAsync(new CreateIndexModel<Category>(
            Builders<Category>.IndexKeys.Ascending(c => c.Name),
            new CreateIndexOptions
            {
                Unique = true,
                Name = "ux_name",
                Collation = new Collation("vi", strength: CollationStrength.Secondary)
            }));

        // books
        await db.Books.Indexes.CreateOneAsync(new CreateIndexModel<Book>(
            Builders<Book>.IndexKeys.Ascending(b => b.CategoryIds),
            new CreateIndexOptions { Name = "ix_categoryIds" }));
        await db.Books.Indexes.CreateOneAsync(new CreateIndexModel<Book>(
            Builders<Book>.IndexKeys.Text(b => b.Title).Text(b => b.Authors),
            new CreateIndexOptions { Name = "tx_title_authors", DefaultLanguage = "none" }));

        // loans
        await db.Loans.Indexes.CreateOneAsync(new CreateIndexModel<Loan>(
            Builders<Loan>.IndexKeys.Ascending(l => l.LoanCode),
            new CreateIndexOptions { Unique = true, Name = "ux_loanCode" }));
        await db.Loans.Indexes.CreateOneAsync(new CreateIndexModel<Loan>(
            Builders<Loan>.IndexKeys.Ascending("reader.readerId").Ascending(l => l.Status),
            new CreateIndexOptions { Name = "ix_reader_status" }));
        await db.Loans.Indexes.CreateOneAsync(new CreateIndexModel<Loan>(
            Builders<Loan>.IndexKeys.Ascending(l => l.Status).Ascending(l => l.DueDate),
            new CreateIndexOptions { Name = "ix_status_dueDate" }));
        await db.Loans.Indexes.CreateOneAsync(new CreateIndexModel<Loan>(
            Builders<Loan>.IndexKeys.Ascending("items.bookId").Ascending(l => l.Status),
            new CreateIndexOptions { Name = "ix_itemsBookId_status" }));

        // reservations
        await db.Reservations.Indexes.CreateOneAsync(new CreateIndexModel<Reservation>(
            Builders<Reservation>.IndexKeys
                .Ascending(r => r.BookId).Ascending(r => r.Status).Ascending(r => r.ReservedAt),
            new CreateIndexOptions { Name = "ix_book_status_reservedAt" }));
        await db.Reservations.Indexes.CreateOneAsync(new CreateIndexModel<Reservation>(
            Builders<Reservation>.IndexKeys.Ascending(r => r.ReaderId).Ascending(r => r.Status),
            new CreateIndexOptions { Name = "ix_reader_status" }));
    }

    // ---------------------------------------------------------------- Seed đầy đủ

    /// <summary>
    /// Seed dữ liệu mẫu đầy đủ (Mục 8 thiết kế kỹ thuật):
    /// 1 Thủ thư, 6 Thể loại, 28 Sách, 10 Độc giả (có thẻ khóa & hết hạn), Phiếu mượn (đang mượn, quá hạn, đã trả) và Đặt trước.
    /// </summary>
    private static async Task SeedAsync(MongoDbContext db, CounterService counters, LibraryRules rules)
    {
        if (await db.Users.Find(_ => true).AnyAsync()) return;

        var now = DateTime.UtcNow;
        var todayVn = DateHelper.TodayVn();

        // 1. Tài khoản Thủ thư (admin / admin123)
        var adminUser = new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = Role.Librarian,
            FullName = "Quản trị viên Thư viện",
            CreatedAt = now
        };
        await db.Users.InsertOneAsync(adminUser);

        // 2. Thể loại sách (6 thể loại phong phú)
        var catIt = new Category { Name = "Công nghệ thông tin", Description = "Lập trình, mạng máy tính, trí tuệ nhân tạo và hệ thống" };
        var catLife = new Category { Name = "Kỹ năng sống", Description = "Phát triển bản thân, tư duy lãnh đạo, quản lý cảm xúc" };
        var catLit = new Category { Name = "Văn học", Description = "Tiểu thuyết, truyện ngắn, tác phẩm kinh điển trong và ngoài nước" };
        var catEcon = new Category { Name = "Kinh tế & Quản trị", Description = "Kinh doanh, tài chính cá nhân, khởi nghiệp và đầu tư" };
        var catSci = new Category { Name = "Khoa học tự nhiên", Description = "Vũ trụ, vật lý, toán học và thế giới tự nhiên" };
        var catHist = new Category { Name = "Lịch sử & Triết học", Description = "Lịch sử Việt Nam, văn minh thế giới và triết học" };

        await db.Categories.InsertManyAsync(new[] { catIt, catLife, catLit, catEcon, catSci, catHist });

        // 3. Đầu sách (28 cuốn sách thực tế, có sách còn và vài sách HẾT HÀNG để test đặt trước)
        Book MakeBook(string title, string[] authors, string publisher, int year, long price, int total, int available, params Category[] cats) => new()
        {
            Title = title,
            Authors = authors.ToList(),
            Publisher = publisher,
            PublishYear = year,
            Price = price,
            CategoryIds = cats.Select(c => c.Id!).ToList(),
            TotalQuantity = total,
            AvailableQuantity = available,
            CreatedAt = now
        };

        var books = new List<Book>
        {
            // Công nghệ thông tin
            MakeBook("Clean Code - Mã Sạch", new[] { "Robert C. Martin" }, "NXB Thông tin và Truyền thông", 2021, 215000, 4, 3, catIt),
            MakeBook("Design Patterns - Mẫu Thiết Kế Hướng Đối Tượng", new[] { "Erich Gamma", "Richard Helm", "Ralph Johnson", "John Vlissides" }, "NXB Khoa học và Kỹ thuật", 2020, 260000, 3, 2, catIt),
            MakeBook("Lập Trình C# 12 & .NET 8 Toàn Tập", new[] { "Mark J. Price" }, "NXB Thế Giới", 2024, 320000, 3, 2, catIt),
            MakeBook("Học Sâu và Ứng Dụng Thực Tế (Deep Learning)", new[] { "Ian Goodfellow", "Yoshua Bengio" }, "NXB Bách Khoa", 2022, 290000, 2, 2, catIt),
            MakeBook("Cơ Sở Dữ Liệu NoSQL & Phân Tích Dữ Liệu Lớn", new[] { "Pramod J. Sadalage", "Martin Fowler" }, "NXB Khoa học và Kỹ thuật", 2023, 195000, 2, 0, catIt), // Hết sách để test đặt trước

            // Kỹ năng sống
            MakeBook("Đắc Nhân Tâm", new[] { "Dale Carnegie" }, "NXB Tổng hợp TP.HCM", 2021, 86000, 5, 4, catLife),
            MakeBook("Tuổi Trẻ Đáng Giá Bao Nhiêu", new[] { "Rosie Nguyễn" }, "NXB Hội Nhà Văn", 2020, 80000, 4, 3, catLife),
            MakeBook("7 Thói Quen Để Thành Đạt", new[] { "Stephen R. Covey" }, "NXB Trẻ", 2022, 160000, 3, 2, catLife),
            MakeBook("Tư Duy Nhanh và Chậm", new[] { "Daniel Kahneman" }, "NXB Thế Giới", 2021, 190000, 3, 2, catLife),
            MakeBook("Dám Bị Ghét", new[] { "Koga Fumitake", "Kishimi Ichiro" }, "NXB Lao Động", 2023, 110000, 3, 0, catLife), // Hết sách để test đặt trước

            // Văn học
            MakeBook("Nhà Giả Kim", new[] { "Paulo Coelho" }, "NXB Hội Nhà Văn", 2020, 79000, 5, 4, catLit),
            MakeBook("Dế Mèn Phiêu Lưu Ký", new[] { "Tô Hoài" }, "NXB Kim Đồng", 2022, 55000, 4, 3, catLit),
            MakeBook("Cho Tôi Xin Một Vé Đi Tuổi Thơ", new[] { "Nguyễn Nhật Ánh" }, "NXB Trẻ", 2021, 75000, 4, 3, catLit),
            MakeBook("Mắt Biếc", new[] { "Nguyễn Nhật Ánh" }, "NXB Trẻ", 2020, 95000, 3, 2, catLit),
            MakeBook("Rừng Na Uy", new[] { "Haruki Murakami" }, "NXB Hội Nhà Văn", 2019, 130000, 3, 2, catLit),
            MakeBook("Không Gia Đình", new[] { "Hector Malot" }, "NXB Văn Học", 2022, 145000, 2, 2, catLit),

            // Kinh tế & Quản trị
            MakeBook("Cha Giàu Cha Nghèo (Rich Dad Poor Dad)", new[] { "Robert T. Kiyosaki" }, "NXB Trẻ", 2021, 125000, 4, 3, catEcon),
            MakeBook("Khởi Nghiệp Tinh Gọn (The Lean Startup)", new[] { "Eric Ries" }, "NXB Tổng hợp TP.HCM", 2020, 155000, 3, 2, catEcon),
            MakeBook("Chiến Lược Đại Dương Xanh", new[] { "W. Chan Kim", "Renée Mauborgne" }, "NXB Lao Động Xã Hội", 2021, 175000, 3, 2, catEcon),
            MakeBook("Tâm Lý Học Về Tiền", new[] { "Morgan Housel" }, "NXB Dân Trí", 2023, 140000, 3, 0, catEcon), // Hết sách

            // Khoa học tự nhiên
            MakeBook("Vũ Trụ Trong Vỏ Hạt Dẻ", new[] { "Stephen Hawking" }, "NXB Trẻ", 2021, 135000, 3, 2, catSci),
            MakeBook("Lược Sử Thời Gian", new[] { "Stephen Hawking" }, "NXB Trẻ", 2020, 120000, 3, 2, catSci),
            MakeBook("Gen: Lịch Sử Và Tương Lai Của Nhân Loại", new[] { "Siddhartha Mukherjee" }, "NXB Dân Trí", 2022, 240000, 2, 2, catSci),
            MakeBook("Nguồn Gốc Các Loài", new[] { "Charles Darwin" }, "NXB Tri Thức", 2019, 180000, 2, 2, catSci),

            // Lịch sử & Triết học
            MakeBook("Sapiens - Lược Sử Loài Người", new[] { "Yuval Noah Harari" }, "NXB Tri Thức", 2021, 220000, 4, 3, catHist),
            MakeBook("Homo Deus - Lược Sử Tương Lai", new[] { "Yuval Noah Harari" }, "NXB Tri Thức", 2022, 230000, 3, 2, catHist),
            MakeBook("Đại Việt Sử Ký Toàn Thư", new[] { "Ngô Sĩ Liên" }, "NXB Khoa học Xã hội", 2020, 380000, 2, 2, catHist),
            MakeBook("Bàn Về Tự Do", new[] { "John Stuart Mill" }, "NXB Tri Thức", 2021, 95000, 2, 2, catHist)
        };
        await db.Books.InsertManyAsync(books);

        // 4. 10 Độc giả kèm tài khoản (tài khoản = mã thẻ, mật khẩu = rules.DefaultPassword "123456")
        var readerSamples = new[]
        {
            (Name: "Nguyễn Văn An", Cccd: "079200000001", Phone: "0901234567", Email: "an.nguyen@example.com", Status: CardStatus.Active, IsExpired: false),
            (Name: "Trần Thị Bình", Cccd: "079200000002", Phone: "0907654321", Email: "binh.tran@example.com", Status: CardStatus.Active, IsExpired: false),
            (Name: "Lê Hoàng Cường", Cccd: "079200000003", Phone: "0912345678", Email: "cuong.le@example.com", Status: CardStatus.Active, IsExpired: false),
            (Name: "Phạm Thị Dung", Cccd: "079200000004", Phone: "0934567890", Email: "dung.pham@example.com", Status: CardStatus.Active, IsExpired: false),
            (Name: "Hoàng Văn Em", Cccd: "079200000005", Phone: "0945678901", Email: "em.hoang@example.com", Status: CardStatus.Active, IsExpired: false),
            (Name: "Đỗ Thị Mai", Cccd: "079200000006", Phone: "0967890123", Email: "mai.do@example.com", Status: CardStatus.Active, IsExpired: false),
            (Name: "Bùi Quang Nhật", Cccd: "079200000007", Phone: "0978901234", Email: "nhat.bui@example.com", Status: CardStatus.Active, IsExpired: false),
            (Name: "Vũ Thị Oanh", Cccd: "079200000008", Phone: "0989012345", Email: "oanh.vu@example.com", Status: CardStatus.Active, IsExpired: false),
            // Độc giả bị khóa thẻ để test chặn mượn
            (Name: "Đặng Minh Phúc", Cccd: "079200000009", Phone: "0918273645", Email: "phuc.dang@example.com", Status: CardStatus.Locked, IsExpired: false),
            // Độc giả bị hết hạn thẻ để test cảnh báo hết hạn
            (Name: "Ngô Thị Quỳnh", Cccd: "079200000010", Phone: "0928374650", Email: "quynh.ngo@example.com", Status: CardStatus.Active, IsExpired: true)
        };

        var readers = new List<Reader>();
        foreach (var s in readerSamples)
        {
            var issuedDate = s.IsExpired ? todayVn.AddYears(-2) : todayVn.AddMonths(-2);
            var expiryDate = s.IsExpired ? todayVn.AddYears(-1) : issuedDate.AddYears(rules.CardValidYears);

            var reader = new Reader
            {
                CardNumber = await counters.NextReaderCodeAsync(),
                FullName = s.Name,
                IdNumber = s.Cccd,
                Phone = s.Phone,
                Email = s.Email,
                Address = "TP. Hồ Chí Minh",
                IssuedDate = DateHelper.VnDateToUtc(issuedDate),
                ExpiryDate = DateHelper.VnDateToUtc(expiryDate),
                CardStatus = s.Status,
                LockReason = s.Status == CardStatus.Locked ? "Vi phạm quy định: làm mất sách chưa đền bù" : null
            };
            await db.Readers.InsertOneAsync(reader);
            readers.Add(reader);

            // Tạo tài khoản cho độc giả
            await db.Users.InsertOneAsync(new User
            {
                Username = reader.CardNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(rules.DefaultPassword),
                Role = Role.Reader,
                ReaderId = reader.Id,
                FullName = reader.FullName,
                CreatedAt = now
            });
        }

        // 5. Phiếu mượn mẫu (Loans)
        var bookCleanCode = books.First(b => b.Title.StartsWith("Clean Code"));
        var bookDacNhanTam = books.First(b => b.Title.StartsWith("Đắc Nhân Tâm"));
        var bookNhaGiaKim = books.First(b => b.Title.StartsWith("Nhà Giả Kim"));

        // Phiếu 1: Đang mượn đúng hạn (DG000001)
        var loan1 = new Loan
        {
            LoanCode = await counters.NextLoanCodeAsync(),
            Reader = new LoanReader { ReaderId = readers[0].Id!, CardNumber = readers[0].CardNumber, FullName = readers[0].FullName },
            LibrarianId = adminUser.Id,
            BorrowDate = DateHelper.VnDateToUtc(todayVn.AddDays(-5)),
            DueDate = DateHelper.VnDateToUtc(todayVn.AddDays(-5 + rules.LoanDays)),
            Status = LoanStatus.Borrowing,
            RenewalCount = 0,
            Items = new List<LoanItem>
            {
                new() { BookId = bookCleanCode.Id!, Title = bookCleanCode.Title, CategoryIds = bookCleanCode.CategoryIds },
                new() { BookId = bookDacNhanTam.Id!, Title = bookDacNhanTam.Title, CategoryIds = bookDacNhanTam.CategoryIds }
            }
        };

        // Phiếu 2: QUÁ HẠN (DG000002) - Mượn cách đây 25 ngày, hạn 14 ngày -> Quá hạn 11 ngày!
        var overdueBorrowDate = todayVn.AddDays(-25);
        var overdueDueDate = overdueBorrowDate.AddDays(rules.LoanDays);
        var loan2 = new Loan
        {
            LoanCode = await counters.NextLoanCodeAsync(),
            Reader = new LoanReader { ReaderId = readers[1].Id!, CardNumber = readers[1].CardNumber, FullName = readers[1].FullName },
            LibrarianId = adminUser.Id,
            BorrowDate = DateHelper.VnDateToUtc(overdueBorrowDate),
            DueDate = DateHelper.VnDateToUtc(overdueDueDate),
            Status = LoanStatus.Borrowing,
            RenewalCount = 0,
            Items = new List<LoanItem>
            {
                new() { BookId = bookNhaGiaKim.Id!, Title = bookNhaGiaKim.Title, CategoryIds = bookNhaGiaKim.CategoryIds }
            }
        };

        // Phiếu 3: Đang mượn VÀ CÓ YÊU CẦU GIA HẠN ONLINE ĐANG CHỜ DUYỆT (DG000003)
        var bookSapiens = books.First(b => b.Title.StartsWith("Sapiens"));
        var loan3 = new Loan
        {
            LoanCode = await counters.NextLoanCodeAsync(),
            Reader = new LoanReader { ReaderId = readers[2].Id!, CardNumber = readers[2].CardNumber, FullName = readers[2].FullName },
            LibrarianId = adminUser.Id,
            BorrowDate = DateHelper.VnDateToUtc(todayVn.AddDays(-7)),
            DueDate = DateHelper.VnDateToUtc(todayVn.AddDays(-7 + rules.LoanDays)),
            Status = LoanStatus.Borrowing,
            RenewalCount = 0,
            Items = new List<LoanItem>
            {
                new() { BookId = bookSapiens.Id!, Title = bookSapiens.Title, CategoryIds = bookSapiens.CategoryIds }
            },
            RenewalRequests = new List<RenewalRequest>
            {
                new()
                {
                    RequestedAt = DateTime.UtcNow.AddHours(-3),
                    Source = RenewalSource.Online,
                    Status = RenewalStatus.Pending
                }
            }
        };

        // Phiếu 4: ĐÃ TRẢ HOÀN TẤT (DG000004)
        var book7Habits = books.First(b => b.Title.StartsWith("7 Thói Quen"));
        var loan4 = new Loan
        {
            LoanCode = await counters.NextLoanCodeAsync(),
            Reader = new LoanReader { ReaderId = readers[3].Id!, CardNumber = readers[3].CardNumber, FullName = readers[3].FullName },
            LibrarianId = adminUser.Id,
            BorrowDate = DateHelper.VnDateToUtc(todayVn.AddDays(-30)),
            DueDate = DateHelper.VnDateToUtc(todayVn.AddDays(-16)),
            Status = LoanStatus.Returned,
            Items = new List<LoanItem>
            {
                new()
                {
                    BookId = book7Habits.Id!,
                    Title = book7Habits.Title,
                    CategoryIds = book7Habits.CategoryIds,
                    ReturnDate = DateHelper.VnDateToUtc(todayVn.AddDays(-17)),
                    Condition = BookCondition.Normal,
                    Fine = 0
                }
            }
        };

        await db.Loans.InsertManyAsync(new[] { loan1, loan2, loan3, loan4 });

        // 6. Yêu cầu đặt trước mẫu (Reservations)
        var outOfStockBook = books.First(b => b.AvailableQuantity == 0);
        var inStockBook = books.First(b => b.AvailableQuantity > 0 && b.Title.StartsWith("Tuổi Trẻ"));

        var res1 = new Reservation
        {
            ReaderId = readers[4].Id!,
            BookId = outOfStockBook.Id!,
            BookTitle = outOfStockBook.Title,
            ReservedAt = DateTime.UtcNow.AddDays(-1),
            Status = ReservationStatus.Pending
        };

        var res2 = new Reservation
        {
            ReaderId = readers[5].Id!,
            BookId = inStockBook.Id!,
            BookTitle = inStockBook.Title,
            ReservedAt = DateTime.UtcNow.AddDays(-2),
            Status = ReservationStatus.Approved,
            ProcessedBy = adminUser.Id
        };

        await db.Reservations.InsertManyAsync(new[] { res1, res2 });
    }

    /// <summary>
    /// Đảm bảo hệ thống có đủ dữ liệu phiếu mượn phong phú (các tháng khác nhau, có phạt trễ hạn, phạt hư hỏng, phạt mất sách)
    /// để các trang báo cáo thống kê Aggregation (UC12) và Dashboard hiển thị trực quan, sinh động.
    /// </summary>
    private static async Task EnsureRichLoansAsync(MongoDbContext db, CounterService counters, LibraryRules rules)
    {
        // Nếu đã có phiếu mượn có tiền phạt rồi thì không cần chèn thêm
        if (await db.Loans.Find(l => l.TotalFine > 0).AnyAsync()) return;

        var readers = await db.Readers.Find(_ => true).Limit(6).ToListAsync();
        var books = await db.Books.Find(_ => true).Limit(10).ToListAsync();
        var admin = await db.Users.Find(u => u.Role == Role.Librarian).FirstOrDefaultAsync();
        if (readers.Count < 4 || books.Count < 5 || admin == null) return;

        var todayVn = DateHelper.TodayVn();
        var year = todayVn.Year;

        // 1. Phiếu phạt trả muộn (Tháng 6): Trả trễ 5 ngày -> Phạt 25.000đ
        var b1 = books[0];
        var borrowDate1 = new DateTime(year, 6, 1, 8, 0, 0, DateTimeKind.Unspecified);
        var dueDate1 = borrowDate1.AddDays(rules.LoanDays);
        var returnDate1 = dueDate1.AddDays(5);
        var fineOverdue = 5 * rules.FinePerDay;

        var loanOverdue = new Loan
        {
            LoanCode = await counters.NextLoanCodeAsync(),
            Reader = new LoanReader { ReaderId = readers[0].Id!, CardNumber = readers[0].CardNumber, FullName = readers[0].FullName },
            LibrarianId = admin.Id,
            BorrowDate = DateHelper.VnDateToUtc(borrowDate1),
            DueDate = DateHelper.VnDateToUtc(dueDate1),
            Status = LoanStatus.Returned,
            TotalFine = fineOverdue,
            Items = new List<LoanItem>
            {
                new()
                {
                    BookId = b1.Id!,
                    Title = b1.Title,
                    CategoryIds = b1.CategoryIds,
                    ReturnDate = DateHelper.VnDateToUtc(returnDate1),
                    Condition = BookCondition.Normal,
                    Fine = fineOverdue
                }
            }
        };

        // 2. Phiếu phạt sách hư hỏng (Tháng 7): Hư hỏng nhẹ -> Phạt 50.000đ
        var b2 = books[1];
        var borrowDate2 = new DateTime(year, 7, 5, 9, 0, 0, DateTimeKind.Unspecified);
        var dueDate2 = borrowDate2.AddDays(rules.LoanDays);
        var returnDate2 = dueDate2;
        var fineDamage = 50000;

        var loanDamage = new Loan
        {
            LoanCode = await counters.NextLoanCodeAsync(),
            Reader = new LoanReader { ReaderId = readers[1].Id!, CardNumber = readers[1].CardNumber, FullName = readers[1].FullName },
            LibrarianId = admin.Id,
            BorrowDate = DateHelper.VnDateToUtc(borrowDate2),
            DueDate = DateHelper.VnDateToUtc(dueDate2),
            Status = LoanStatus.Returned,
            TotalFine = fineDamage,
            Items = new List<LoanItem>
            {
                new()
                {
                    BookId = b2.Id!,
                    Title = b2.Title,
                    CategoryIds = b2.CategoryIds,
                    ReturnDate = DateHelper.VnDateToUtc(returnDate2),
                    Condition = BookCondition.Damaged,
                    Fine = fineDamage
                }
            }
        };

        // 3. Phiếu phạt làm mất sách (Tháng 8): Đền 100% giá sách
        var b3 = books[2];
        var borrowDate3 = new DateTime(year, 8, 10, 10, 0, 0, DateTimeKind.Unspecified);
        var dueDate3 = borrowDate3.AddDays(rules.LoanDays);
        var returnDate3 = dueDate3;
        var fineLost = b3.Price;

        var loanLost = new Loan
        {
            LoanCode = await counters.NextLoanCodeAsync(),
            Reader = new LoanReader { ReaderId = readers[2].Id!, CardNumber = readers[2].CardNumber, FullName = readers[2].FullName },
            LibrarianId = admin.Id,
            BorrowDate = DateHelper.VnDateToUtc(borrowDate3),
            DueDate = DateHelper.VnDateToUtc(dueDate3),
            Status = LoanStatus.Returned,
            TotalFine = fineLost,
            Items = new List<LoanItem>
            {
                new()
                {
                    BookId = b3.Id!,
                    Title = b3.Title,
                    CategoryIds = b3.CategoryIds,
                    ReturnDate = DateHelper.VnDateToUtc(returnDate3),
                    Condition = BookCondition.Lost,
                    Fine = fineLost
                }
            }
        };

        // 4. Phiếu mượn tháng 9 (mượn 2 cuốn trả đúng hạn bình thường)
        var b4 = books[3];
        var b5 = books[0];
        var borrowDate4 = new DateTime(year, 9, 12, 14, 0, 0, DateTimeKind.Unspecified);
        var dueDate4 = borrowDate4.AddDays(rules.LoanDays);
        var returnDate4 = dueDate4.AddDays(-2);

        var loanSept = new Loan
        {
            LoanCode = await counters.NextLoanCodeAsync(),
            Reader = new LoanReader { ReaderId = readers[3].Id!, CardNumber = readers[3].CardNumber, FullName = readers[3].FullName },
            LibrarianId = admin.Id,
            BorrowDate = DateHelper.VnDateToUtc(borrowDate4),
            DueDate = DateHelper.VnDateToUtc(dueDate4),
            Status = LoanStatus.Returned,
            TotalFine = 0,
            Items = new List<LoanItem>
            {
                new() { BookId = b4.Id!, Title = b4.Title, CategoryIds = b4.CategoryIds, ReturnDate = DateHelper.VnDateToUtc(returnDate4), Condition = BookCondition.Normal, Fine = 0 },
                new() { BookId = b5.Id!, Title = b5.Title, CategoryIds = b5.CategoryIds, ReturnDate = DateHelper.VnDateToUtc(returnDate4), Condition = BookCondition.Normal, Fine = 0 }
            }
        };

        // 5. Phiếu mượn tháng 10 (mượn 2 cuốn trả đúng hạn bình thường)
        var b6 = books[1];
        var b7 = books[4];
        var borrowDate5 = new DateTime(year, 10, 5, 11, 0, 0, DateTimeKind.Unspecified);
        var dueDate5 = borrowDate5.AddDays(rules.LoanDays);
        var returnDate5 = dueDate5.AddDays(-1);

        var loanOct = new Loan
        {
            LoanCode = await counters.NextLoanCodeAsync(),
            Reader = new LoanReader { ReaderId = readers[0].Id!, CardNumber = readers[0].CardNumber, FullName = readers[0].FullName },
            LibrarianId = admin.Id,
            BorrowDate = DateHelper.VnDateToUtc(borrowDate5),
            DueDate = DateHelper.VnDateToUtc(dueDate5),
            Status = LoanStatus.Returned,
            TotalFine = 0,
            Items = new List<LoanItem>
            {
                new() { BookId = b6.Id!, Title = b6.Title, CategoryIds = b6.CategoryIds, ReturnDate = DateHelper.VnDateToUtc(returnDate5), Condition = BookCondition.Normal, Fine = 0 },
                new() { BookId = b7.Id!, Title = b7.Title, CategoryIds = b7.CategoryIds, ReturnDate = DateHelper.VnDateToUtc(returnDate5), Condition = BookCondition.Normal, Fine = 0 }
            }
        };

        await db.Loans.InsertManyAsync(new[] { loanOverdue, loanDamage, loanLost, loanSept, loanOct });
    }

    /// <summary>
    /// Tự động xuất database ra file JSON Extended Canonical trong Data/MongoData để đồng bộ giữa các thành viên.
    /// </summary>
    public static async Task ExportMongoDataAsync(MongoDbContext db, IWebHostEnvironment env)
    {
        try
        {
            var outDir = Path.Combine(env.ContentRootPath, "Data", "MongoData");
            if (!Directory.Exists(outDir)) return;

            var jsonSettings = new MongoDB.Bson.IO.JsonWriterSettings
            {
                OutputMode = MongoDB.Bson.IO.JsonOutputMode.CanonicalExtendedJson
            };

            var collections = new[] { "books", "categories", "counters", "loans", "readers", "reservations", "users" };

            foreach (var colName in collections)
            {
                var col = db.Database.GetCollection<BsonDocument>(colName);
                var docs = await col.Find(_ => true).Sort(new BsonDocument("_id", 1)).ToListAsync();
                var lines = docs.Select(d => d.ToJson(jsonSettings)).ToList();
                var filePath = Path.Combine(outDir, $"{colName}.json");
                await File.WriteAllTextAsync(filePath, string.Join("\n", lines) + (lines.Count > 0 ? "\n" : ""), System.Text.Encoding.UTF8);
            }
        }
        catch
        {
            // Bỏ qua nếu môi trường không cho phép ghi file
        }
    }
}
