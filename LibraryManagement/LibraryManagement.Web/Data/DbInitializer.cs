using Microsoft.Extensions.Options;
using MongoDB.Driver;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Options;
using LibraryManagement.Web.Services;

namespace LibraryManagement.Web.Data;

/// <summary>
/// Chạy khi ứng dụng khởi động: tạo index (mục 5.4) rồi seed dữ liệu.
/// - CreateIndexesAsync: do A viết ở bước 0.
/// - SeedAsync: A chỉ seed tối thiểu; sau bước 0 giao cho B viết seed đầy đủ (mục 8 thiết kế kỹ thuật).
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

        await CreateIndexesAsync(db);
        await SeedAsync(
            db,
            scope.ServiceProvider.GetRequiredService<CounterService>(),
            scope.ServiceProvider.GetRequiredService<IOptions<LibraryRules>>().Value);
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

    // ---------------------------------------------------------------- Seed tối thiểu

    /// <summary>Chỉ seed khi collection users đang rỗng. B sẽ thay phần này bằng seed đầy đủ.</summary>
    private static async Task SeedAsync(MongoDbContext db, CounterService counters, LibraryRules rules)
    {
        if (await db.Users.Find(_ => true).AnyAsync()) return;

        var now = DateTime.UtcNow;

        // 1 thủ thư
        await db.Users.InsertOneAsync(new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = Role.Librarian,
            FullName = "Quản trị viên",
            CreatedAt = now
        });

        // 2 thể loại
        var skills = new Category { Name = "Kỹ năng sống", Description = "Sách phát triển bản thân" };
        var literature = new Category { Name = "Văn học", Description = "Tiểu thuyết, truyện ngắn" };
        await db.Categories.InsertManyAsync(new[] { skills, literature });

        // 5 sách (chưa có phiếu mượn nên availableQuantity = totalQuantity)
        Book NewBook(string title, string author, string publisher, int year, long price, int qty, Category category) => new()
        {
            Title = title,
            Authors = new List<string> { author },
            Publisher = publisher,
            PublishYear = year,
            Price = price,
            CategoryIds = new List<string> { category.Id! },
            TotalQuantity = qty,
            AvailableQuantity = qty,
            CreatedAt = now
        };
        await db.Books.InsertManyAsync(new[]
        {
            NewBook("Đắc Nhân Tâm", "Dale Carnegie", "NXB Tổng hợp TP.HCM", 2020, 86000, 3, skills),
            NewBook("Nhà Giả Kim", "Paulo Coelho", "NXB Hội Nhà Văn", 2018, 79000, 2, literature),
            NewBook("Tuổi Trẻ Đáng Giá Bao Nhiêu", "Rosie Nguyễn", "NXB Hội Nhà Văn", 2019, 80000, 2, skills),
            NewBook("Dế Mèn Phiêu Lưu Ký", "Tô Hoài", "NXB Kim Đồng", 2021, 55000, 4, literature),
            NewBook("Cho Tôi Xin Một Vé Đi Tuổi Thơ", "Nguyễn Nhật Ánh", "NXB Trẻ", 2017, 70000, 1, literature)
        });

        // 2 độc giả kèm tài khoản (tên đăng nhập = mã thẻ, mật khẩu = DefaultPassword)
        var issued = DateHelper.TodayVn();
        var expiry = issued.AddYears(rules.CardValidYears);
        var samples = new[]
        {
            (Name: "Nguyễn Văn An", Cccd: "079200000001", Phone: "0901234567", Email: "an@example.com"),
            (Name: "Trần Thị Bình", Cccd: "079200000002", Phone: "0907654321", Email: "binh@example.com")
        };
        foreach (var s in samples)
        {
            var reader = new Reader
            {
                CardNumber = await counters.NextReaderCodeAsync(),
                FullName = s.Name,
                IdNumber = s.Cccd,
                Phone = s.Phone,
                Email = s.Email,
                IssuedDate = DateHelper.VnDateToUtc(issued),
                ExpiryDate = DateHelper.VnDateToUtc(expiry),
                CardStatus = CardStatus.Active
            };
            await db.Readers.InsertOneAsync(reader);

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
    }
}
