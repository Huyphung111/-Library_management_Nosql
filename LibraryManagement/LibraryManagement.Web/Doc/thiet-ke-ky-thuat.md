# Thiết kế kỹ thuật: Hệ thống quản lý mượn trả sách

Tài liệu này dùng khi code. Nghiệp vụ và đặc tả use case nằm trong `dac-ta-use-case.md`.

## 1. Công nghệ sử dụng

| Thành phần | Công nghệ | Ghi chú |
|---|---|---|
| Ngôn ngữ, framework | C#, **ASP.NET Core MVC** trên **.NET 10 (LTS)** | .NET 8 đã hết hỗ trợ từ 11/2026 |
| Cơ sở dữ liệu | **MongoDB 8.x Community** (máy đang cài 8.3) | Chạy dạng replica set 1 node để dùng được transaction (xem mục 7.1) |
| Thư viện kết nối | **MongoDB.Driver** (NuGet, bản 3.x) | Driver chính thức của MongoDB cho C# |
| Băm mật khẩu | **BCrypt.Net-Next** (NuGet) | Không lưu mật khẩu dạng chữ thường |
| Xác thực, phân quyền | **Cookie Authentication** có sẵn trong ASP.NET Core | Phân quyền theo role `Librarian`, `Reader` |
| Giao diện | Razor Views, **Bootstrap 5** (có sẵn trong template), **Bootstrap Icons** | Icons nhúng qua CDN jsDelivr. Quy ước giao diện ở mục 6.5 |
| Biểu đồ thống kê | **Chart.js** | Nhúng qua CDN hoặc LibMan |
| Công cụ | Visual Studio 2026 hoặc VS Code + C# Dev Kit, **MongoDB Compass**, mongosh | Compass dùng để xem dữ liệu và thử aggregation |

Khởi tạo project:

```bash
dotnet new sln -n LibraryManagement
dotnet new mvc -n LibraryManagement.Web
dotnet sln add LibraryManagement.Web
cd LibraryManagement.Web
dotnet add package MongoDB.Driver
dotnet add package BCrypt.Net-Next
```

## 2. Cấu trúc thư mục

Đồ án dùng **một project MVC**, chia lớp bằng thư mục: Controller gọi Service, Service làm việc với MongoDB. Không dùng Area: controller của cả hai vai trò nằm chung thư mục `Controllers/`, phân quyền bằng `[Authorize(Roles = "...")]` đặt trên từng controller.

```
LibraryManagement/
├── LibraryManagement.slnx              # .NET 10 tạo định dạng .slnx thay cho .sln
└── LibraryManagement.Web/
    ├── Program.cs                      # Đăng ký DI, MongoDB, cookie auth, route
    ├── appsettings.json                # Chuỗi kết nối + quy định nghiệp vụ
    │
    ├── Data/
    │   ├── MongoDbSettings.cs          # Map từ appsettings
    │   ├── MongoDbContext.cs           # Giữ IMongoDatabase, expose các collection
    │   ├── MongoConventions.cs         # camelCase, enum lưu dạng string
    │   └── DbInitializer.cs            # Tạo index + seed dữ liệu mẫu khi chạy lần đầu
    │
    ├── Models/                         # Class ánh xạ document MongoDB
    │   ├── User.cs
    │   ├── Reader.cs
    │   ├── Book.cs
    │   ├── Category.cs
    │   ├── Loan.cs                     # Gồm LoanItem, RenewalRequest (nhúng)
    │   ├── Reservation.cs
    │   ├── Counter.cs
    │   └── Enums.cs                    # Role, CardStatus, LoanStatus, ...
    │
    ├── Options/
    │   └── LibraryRules.cs             # Số sách tối đa, số ngày mượn, mức phạt...
    │
    ├── Services/                       # Toàn bộ quy định nghiệp vụ nằm ở đây
    │   ├── AuthService.cs              # UC01, UC03
    │   ├── CategoryService.cs          # UC05
    │   ├── BookService.cs              # UC04, UC13
    │   ├── ReaderService.cs            # UC06
    │   ├── LoanService.cs              # UC07, UC08, UC09, UC14, UC15
    │   ├── ReservationService.cs       # UC10, UC16
    │   ├── ReportService.cs            # UC12 (aggregation pipeline)
    │   ├── SearchService.cs            # UC11
    │   ├── CounterService.cs           # Sinh mã thẻ, mã phiếu tự tăng
    │   └── ServiceResult.cs            # Kết quả trả về: thành công / lỗi + thông báo
    │
    ├── ViewModels/                     # Dữ liệu cho form và màn hình
    │   ├── Account/                    # LoginVM, ChangePasswordVM
    │   ├── Books/                      # BookFormVM, BookListVM, BookSearchVM
    │   ├── Readers/                    # ReaderFormVM, ReaderListVM
    │   ├── Loans/                      # CreateLoanVM, ReturnLoanVM, RenewalVM
    │   ├── Reservations/
    │   └── Reports/
    │
    ├── Controllers/
    │   ├── HomeController.cs           # Chuyển hướng theo role sau đăng nhập
    │   ├── AccountController.cs        # UC01–UC03, dùng chung 2 vai trò
    │   │
    │   │   # Thủ thư: [Authorize(Roles = "Librarian")]
    │   ├── DashboardController.cs
    │   ├── BooksController.cs          # UC04
    │   ├── CategoriesController.cs     # UC05
    │   ├── ReadersController.cs        # UC06
    │   ├── LoansController.cs          # UC07, UC08
    │   ├── RenewalsController.cs       # UC09
    │   ├── ReservationsController.cs   # UC10
    │   ├── SearchController.cs         # UC11
    │   ├── ReportsController.cs        # UC12
    │   │
    │   │   # Độc giả: [Authorize(Roles = "Reader")]
    │   ├── CatalogController.cs        # UC13
    │   ├── MyLoansController.cs        # UC14, UC15
    │   └── MyReservationsController.cs # UC16
    │
    ├── Views/
    │   ├── Account/                    # Login, ChangePassword, AccessDenied
    │   ├── Dashboard/  Books/  Categories/  Readers/  Loans/
    │   ├── Renewals/  Reservations/  Search/  Reports/
    │   ├── Catalog/  MyLoans/  MyReservations/
    │   └── Shared/                     # _Layout, _LibrarianMenu, _ReaderMenu, _Alert
    │
    └── wwwroot/                        # css, js, lib (bootstrap, chart.js)
```

## 3. Ánh xạ Use Case sang Controller

| UC | Use case | Controller | Action chính |
|---|---|---|---|
| UC01 | Đăng nhập | `AccountController` | `Login` (GET, POST) |
| UC02 | Đăng xuất | `AccountController` | `Logout` (POST) |
| UC03 | Đổi mật khẩu | `AccountController` | `ChangePassword` (GET, POST) |
| UC04 | Quản lý sách | `BooksController` | `Index`, `Create`, `Edit`, `Delete` |
| UC05 | Quản lý thể loại | `CategoriesController` | `Index`, `Create`, `Edit`, `Delete` |
| UC06 | Quản lý độc giả | `ReadersController` | `Index`, `Create`, `Edit`, `Lock`, `Unlock`, `ExtendCard`, `ResetPassword` |
| UC07 | Lập phiếu mượn | `LoansController` | `Create` (GET, POST), `CheckReader` (AJAX, trả về partial `_Borrower`; tắt JavaScript thì dùng `Create?cardNumber=`), `Details` (chi tiết phiếu, dùng chung) |
| UC08 | Nhận trả sách | `LoansController` | `Return` (GET, POST), `ReturnResult` (kết quả và danh sách người chờ) |
| UC09 | Gia hạn phiếu mượn | `RenewalsController` | `Index`, `Approve`, `Reject`, `RenewAtCounter` |
| UC10 | Xử lý đặt trước | `ReservationsController` | `Index`, `Approve`, `Reject`, `Cancel` |
| UC11 | Tìm kiếm | `SearchController` | `Index` (tham số `type` = book / reader / loan) |
| UC12 | Thống kê | `ReportsController` | `TopBooks`, `Overdue`, `ByMonth`, `ByCategory`, `Fines` |
| UC13 | Tìm kiếm sách | `CatalogController` | `Index`, `Details` |
| UC14 | Xem sách đang mượn, lịch sử | `MyLoansController` | `Index`, `History` |
| UC15 | Yêu cầu gia hạn | `MyLoansController` | `RequestRenewal` (POST) |
| UC16 | Đặt trước sách | `MyReservationsController` | `Index`, `Create` (POST), `Cancel` (POST) |

Mọi action POST đều dùng `[ValidateAntiForgeryToken]`.

## 4. Cấu hình

`appsettings.json`:

```json
{
  "MongoDb": {
    "ConnectionString": "mongodb://localhost:27017/?replicaSet=rs0",
    "DatabaseName": "library_management"
  },
  "LibraryRules": {
    "CardValidYears": 1,
    "MaxBooksPerReader": 5,
    "LoanDays": 14,
    "MaxRenewals": 2,
    "RenewalDays": 7,
    "FinePerDay": 5000,
    "MinPasswordLength": 6,
    "DefaultPassword": "123456"
  }
}
```

Mọi quy định QĐ01–QĐ10 đọc từ `LibraryRules` qua `IOptions<LibraryRules>`. Không viết cứng các con số trong code.

## 5. Thiết kế database

Database: `library_management`. Tên field trong MongoDB dùng **camelCase**, enum lưu dạng **string** để đọc được trong Compass.

### 5.1. Danh sách collection

| Collection | Lưu gì |
|---|---|
| `users` | Tài khoản đăng nhập |
| `readers` | Hồ sơ độc giả, thẻ thư viện |
| `categories` | Thể loại |
| `books` | Đầu sách, tác giả nhúng dạng mảng |
| `loans` | Phiếu mượn; các cuốn sách, tiền phạt và yêu cầu gia hạn nhúng bên trong |
| `reservations` | Yêu cầu đặt trước |
| `counters` | Bộ đếm sinh mã thẻ `DG000001`, mã phiếu `PM000001` |

`counters` là collection kỹ thuật, không phải dữ liệu nghiệp vụ. Mỗi lần cần mã mới, gọi `FindOneAndUpdate` với `$inc` để tăng số đếm một cách nguyên tử.

### 5.2. Cấu trúc document

**users**
```json
{
  "_id": ObjectId,
  "username": "DG000001",
  "passwordHash": "$2a$11$...",
  "role": "Reader",                 // Librarian | Reader
  "readerId": ObjectId,             // null nếu là thủ thư
  "fullName": "Nguyễn Văn A",
  "createdAt": ISODate
}
```

**readers**
```json
{
  "_id": ObjectId,
  "cardNumber": "DG000001",
  "fullName": "Nguyễn Văn A",
  "dateOfBirth": ISODate,
  "idNumber": "079200000001",       // CCCD
  "phone": "0901234567",
  "email": "a@gmail.com",
  "address": "...",
  "issuedDate": ISODate,
  "expiryDate": ISODate,
  "cardStatus": "Active",           // Active | Locked
  "lockReason": null
}
```

**categories**
```json
{ "_id": ObjectId, "name": "Kỹ năng sống", "description": "..." }
```

**books**
```json
{
  "_id": ObjectId,
  "title": "Đắc Nhân Tâm",
  "authors": ["Dale Carnegie"],
  "publisher": "NXB Tổng hợp TP.HCM",
  "publishYear": 2020,
  "price": 86000,
  "categoryIds": [ObjectId],
  "totalQuantity": 3,
  "availableQuantity": 1,
  "createdAt": ISODate
}
```

**loans**
```json
{
  "_id": ObjectId,
  "loanCode": "PM000001",
  "reader": {                       // nhúng bản sao để hiển thị, không cần $lookup
    "readerId": ObjectId,
    "cardNumber": "DG000001",
    "fullName": "Nguyễn Văn A"
  },
  "librarianId": ObjectId,
  "borrowDate": ISODate,
  "dueDate": ISODate,
  "status": "Borrowing",            // Borrowing | Returned
  "renewalCount": 0,
  "items": [
    {
      "bookId": ObjectId,
      "title": "Đắc Nhân Tâm",      // bản sao tên sách
      "categoryIds": [ObjectId],    // bản sao, dùng cho thống kê theo thể loại
      "returnDate": null,
      "condition": null,            // Normal | Damaged | Lost
      "fine": 0
    }
  ],
  "totalFine": 0,
  "renewalRequests": [
    {
      "requestedAt": ISODate,
      "source": "Online",           // Online | Counter
      "status": "Pending",          // Pending | Approved | Rejected
      "processedAt": null,
      "processedBy": null,
      "rejectReason": null
    }
  ]
}
```

**reservations**
```json
{
  "_id": ObjectId,
  "readerId": ObjectId,
  "bookId": ObjectId,
  "bookTitle": "Đắc Nhân Tâm",
  "reservedAt": ISODate,
  "status": "Pending",              // Pending | Approved | Rejected | Completed | Cancelled
  "processedBy": null,
  "reason": null,                   // lý do từ chối hoặc hủy
  "completedLoanId": null           // phiếu mượn đã hoàn tất yêu cầu này
}
```

### 5.3. Enum và trạng thái tương ứng

| Enum | Giá trị | Hiển thị |
|---|---|---|
| `Role` | `Librarian`, `Reader` | Thủ thư, Độc giả |
| `CardStatus` | `Active`, `Locked` | Hoạt động, Bị khóa |
| `LoanStatus` | `Borrowing`, `Returned` | Đang mượn, Đã trả |
| `BookCondition` | `Normal`, `Damaged`, `Lost` | Bình thường, Hư hỏng, Mất |
| `RenewalStatus` | `Pending`, `Approved`, `Rejected` | Chờ duyệt, Đã duyệt, Từ chối |
| `ReservationStatus` | `Pending`, `Approved`, `Rejected`, `Completed`, `Cancelled` | Chờ duyệt, Đã duyệt, Từ chối, Hoàn tất, Đã hủy |

"Quá hạn" và "thẻ hết hạn" **không lưu trong database**, mà tính khi truy vấn:
- Phiếu quá hạn: `status = Borrowing` và `dueDate < hôm nay`.
- Thẻ hết hạn: `expiryDate < hôm nay`.

### 5.4. Index

Tạo trong `DbInitializer` khi ứng dụng khởi động:

| Collection | Index | Mục đích |
|---|---|---|
| `users` | `{ username: 1 }` unique | Đăng nhập, chống trùng |
| `readers` | `{ cardNumber: 1 }` unique | Tra cứu theo mã thẻ |
| `readers` | `{ idNumber: 1 }` unique | Chống trùng CCCD |
| `categories` | `{ name: 1 }` unique, collation `vi` strength 2 | Chống trùng tên, không phân biệt hoa thường |
| `books` | `{ categoryIds: 1 }` | Lọc theo thể loại, kiểm tra trước khi xóa thể loại |
| `books` | text index trên `title`, `authors`, `default_language: "none"` | Tìm kiếm sách |
| `loans` | `{ loanCode: 1 }` unique | Tra cứu phiếu |
| `loans` | `{ "reader.readerId": 1, status: 1 }` | Sách đang mượn của độc giả, kiểm tra điều kiện mượn |
| `loans` | `{ status: 1, dueDate: 1 }` | Danh sách phiếu quá hạn |
| `loans` | `{ "items.bookId": 1, status: 1 }` | Kiểm tra trước khi xóa sách |
| `reservations` | `{ bookId: 1, status: 1, reservedAt: 1 }` | Tìm người chờ sớm nhất khi sách được trả |
| `reservations` | `{ readerId: 1, status: 1 }` | "Đặt trước của tôi" |

Text index với `default_language: "none"` giữ nguyên tiếng Việt, không tách gốc từ theo tiếng Anh. Nếu cần tìm kiếm không dấu, thêm field `searchText` lưu bản không dấu của tên sách và tác giả.

## 6. Các điểm cài đặt chính

### 6.1. Program.cs

```csharp
MongoConventions.Register();   // phải gọi trước khi dùng collection

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<LibraryRules>(builder.Configuration.GetSection("LibraryRules"));

builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.ConnectionString));
builder.Services.AddSingleton<MongoDbContext>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<LoanService>();
// ... các service khác

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

// ...
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

await DbInitializer.InitializeAsync(app.Services);   // tạo index + seed
```

### 6.2. MongoConventions

```csharp
var pack = new ConventionPack
{
    new CamelCaseElementNameConvention(),
    new EnumRepresentationConvention(BsonType.String),
    new IgnoreExtraElementsConvention(true)
};
ConventionRegistry.Register("LibraryConventions", pack, _ => true);
```

### 6.3. Claims khi đăng nhập

Khi đăng nhập thành công, lưu vào cookie các claim `NameIdentifier` (userId), `Name` (fullName), `Role`, và với độc giả thêm claim `ReaderId`. Các controller của độc giả (`Catalog`, `MyLoans`, `MyReservations`) lấy `ReaderId` từ claim, không nhận từ form, để độc giả không xem được dữ liệu của người khác.

Nếu thẻ của độc giả đang bị khóa hoặc đã hết hạn lúc đăng nhập (UC01, bước 5a), thêm claim `CardNotice` chứa câu thông báo, ví dụ "Thẻ của bạn đã hết hạn ngày 01/10/2026...". `_Layout` hiện câu này thành thanh cảnh báo dưới thanh trên cùng. Claim chỉ để hiển thị; điều kiện mượn, gia hạn, đặt trước vẫn kiểm tra lại trong Service.

### 6.4. Thời gian

Lưu mọi ngày giờ dạng **UTC**. Khi tính số ngày quá hạn và so sánh hạn trả, quy về **ngày theo giờ Việt Nam** (`SE Asia Standard Time` trên Windows). Viết hàm tính ngày ở một chỗ duy nhất, ví dụ `DateHelper.TodayVn()`.

Quy ước lưu (đã cài trong `Services/DateHelper.cs`):
- Các field kiểu **ngày** (`borrowDate`, `dueDate`, `issuedDate`, `expiryDate`, `dateOfBirth`) lưu là thời điểm **00:00 giờ Việt Nam** của ngày đó, quy ra UTC (tức 17:00 UTC của ngày hôm trước). Dùng `DateHelper.VnDateToUtc(ngày)` để lưu, `DateHelper.ToVnDate(utc)` để đọc.
- Các field kiểu **thời điểm** (`createdAt`, `requestedAt`, `reservedAt`, `processedAt`, `returnDate`) lưu `DateTime.UtcNow`.
- Phiếu quá hạn: `status = Borrowing` và `dueDate < DateHelper.StartOfTodayVnUtc()`. Số ngày quá hạn: `DateHelper.DaysOverdue(dueDate)`.
- Thẻ hết hạn: `expiryDate < DateHelper.StartOfTodayVnUtc()`.

Các quy ước kiểu dữ liệu khác:
- **Id** trong C# là `string` kèm `[BsonRepresentation(BsonType.ObjectId)]`, nên trong MongoDB vẫn là `ObjectId` và model binding, claim dùng được trực tiếp. Riêng `counters` có `_id` là chuỗi tên bộ đếm.
- **Tiền** (`price`, `fine`, `totalFine`, `FinePerDay`) là `long`, đơn vị đồng, không có phần lẻ.

### 6.5. Giao diện dùng chung

Bố cục tham khảo trang quản trị của đồ án cũ Shop-Flower (`_AdminLayout`), đổi màu cho hợp thư viện. Toàn bộ màu nằm trong biến CSS ở đầu `wwwroot/css/site.css`.

| Thành phần | Quy ước |
|---|---|
| Bảng màu | Chính xanh navy `#1e3a5f`, nhấn vàng đồng `#c8963e`, nền `#f5f6fa`, khung nội dung màu trắng |
| `_Layout` (đã đăng nhập) | Sidebar trái 250px nạp `_LibrarianMenu` hoặc `_ReaderMenu` theo role; thanh trên có nút thu gọn sidebar và dropdown tài khoản (Đổi mật khẩu, Đăng xuất); thanh cảnh báo `CardNotice` cho độc giả (mục 6.3); `_Alert`; tiêu đề trang |
| `_Layout` (chưa đăng nhập) | Không có sidebar. Nền navy, một khung trắng ở giữa có logo; view chỉ viết phần form |
| Tiêu đề trang | Lấy từ `ViewData["Title"]`. Nút hành động bên phải tiêu đề đặt trong `@section PageActions { ... }` |
| Khung nội dung | `<div class="content-card">` cho bảng, form, khung thông tin |
| Thẻ số liệu | `.stat-card` gồm ô icon màu, con số, chú thích (Dashboard, khung kiểm tra độc giả ở UC07) |
| Lưới thẻ sách (UC13) | `<div class="book-grid">` chứa các `<div class="book-card">`: `book-cover` (thêm `alt` để đổi màu vàng đồng, xen kẽ) → `book-body` gồm `cat-tag` (thể loại), `book-title`, `book-meta` (tác giả, NXB · năm), `book-foot` gồm `<span class="stock in">Còn x/y</span>` hoặc `<span class="stock out">Đã hết</span>` và nút. Dòng "Tìm thấy N đầu sách" dùng `result-info` |
| Badge trạng thái | `<span class="badge status-{giá trị enum viết thường}">@x.ToVn()</span>`. Hai trạng thái tính khi truy vấn dùng `status-overdue` (Quá hạn) và `status-expired` (Thẻ hết hạn) |
| Hiển thị | Ngày `dd/MM/yyyy`, tiền `15.000đ`. Thao tác khóa, xóa, từ chối phải hỏi xác nhận |

## 7. Các xử lý quan trọng với MongoDB

### 7.1. Transaction

Lập phiếu mượn và nhận trả sách cập nhật nhiều document cùng lúc (`loans`, `books`, `reservations`). Cần dùng **transaction** để không bị lệch số lượng khi một bước lỗi. MongoDB chỉ hỗ trợ transaction khi chạy **replica set**, kể cả khi chỉ có 1 node.

Bật replica set 1 node cho MongoDB cài trên Windows:
1. Mở `C:\Program Files\MongoDB\Server\8.3\bin\mongod.cfg` và thêm:
   ```yaml
   replication:
     replSetName: rs0
   ```
2. Khởi động lại service MongoDB.
3. Mở `mongosh` và chạy `rs.initiate()` một lần.

Dùng trong service:

```csharp
using var session = await _client.StartSessionAsync();
await session.WithTransactionAsync(async (s, ct) =>
{
    // trừ số lượng sách, tạo phiếu, cập nhật đặt trước...
    return true;
});
```

Cách khác là dùng MongoDB Atlas bản miễn phí (M0), vốn đã là replica set.

### 7.2. Trừ số lượng sách an toàn

Không đọc số còn lại rồi mới ghi. Gộp điều kiện vào bộ lọc của lệnh update để hai thủ thư cho mượn cùng lúc không làm số còn lại bị âm:

```csharp
var result = await _db.Books.UpdateOneAsync(
    session,
    b => b.Id == bookId && b.AvailableQuantity > 0,
    Builders<Book>.Update.Inc(b => b.AvailableQuantity, -1));

if (result.ModifiedCount == 0) throw new BusinessException("Sách đã hết.");
```

### 7.3. Luồng xử lý theo use case

| UC | Các bước trong service |
|---|---|
| UC07 Lập phiếu mượn | Kiểm tra thẻ (`cardStatus`, `expiryDate`) → đếm phiếu quá hạn của độc giả → đếm số sách đang mượn (`$unwind` items chưa trả) → trong transaction: trừ `availableQuantity` từng sách → tạo loan → `UpdateMany` các reservation `Pending`/`Approved` của độc giả cho các sách này sang `Completed` |
| UC08 Nhận trả sách | Trong transaction: cập nhật từng item (`returnDate`, `condition`, `fine`) bằng `arrayFilters` → sách không mất: `$inc availableQuantity +1`; sách mất: `$inc totalQuantity -1` → cập nhật `totalFine`, chuyển loan sang `Returned` nếu đã trả hết. Sau transaction: tìm reservation `Approved` của các sách vừa trả, sắp theo `reservedAt`, đưa ra màn hình để nhắc Thủ thư |
| UC09 Gia hạn | Kiểm tra điều kiện → `$inc renewalCount`, cộng `dueDate`, cập nhật phần tử `renewalRequests` đang `Pending` bằng positional operator `$` |
| UC10 Xử lý đặt trước | Lấy danh sách reservation, `$lookup` sang `books` để đánh dấu yêu cầu `Approved` nào có sách `availableQuantity > 0` → duyệt, từ chối, hủy chỉ là đổi `status` kèm `reason` |
| UC15 Yêu cầu gia hạn | `$push` một phần tử vào `renewalRequests`, với filter bảo đảm chưa có phần tử `Pending` |
| UC16 Đặt trước sách | Kiểm tra `availableQuantity == 0`, chưa có reservation còn hiệu lực cho sách này, chưa mượn sách này → insert |

### 7.4. Aggregation pipeline cho thống kê (UC12)

| Thống kê | Pipeline |
|---|---|
| Top sách mượn nhiều | `$match` (borrowDate trong khoảng) → `$unwind: "$items"` → `$group` theo `items.bookId`, đếm → `$sort` giảm dần → `$limit 10` |
| Phiếu quá hạn | `$match` `{ status: "Borrowing", dueDate: { $lt: today } }` → `$addFields` số ngày quá hạn → `$sort` |
| Lượt mượn theo tháng | `$match` theo năm → `$group` theo `{ $month: "$borrowDate" }` → `$sort` |
| Lượt mượn theo thể loại | `$unwind: "$items"` → `$unwind: "$items.categoryIds"` → `$group` theo thể loại → `$lookup` sang `categories` lấy tên |
| Tổng tiền phạt | `$match` theo khoảng ngày → `$group` theo tháng với `$sum: "$totalFine"` |

Thử pipeline trong tab Aggregations của Compass trước, rồi mới chuyển sang C# bằng `Aggregate()` hoặc `BsonDocument[]`.

## 8. Seed dữ liệu mẫu

`DbInitializer` chỉ seed khi collection `users` đang rỗng:
- 1 tài khoản thủ thư: `admin` / `admin123`.
- 5–6 thể loại.
- Khoảng 30 đầu sách.
- 10 độc giả kèm tài khoản, trong đó có 1 thẻ bị khóa và 1 thẻ hết hạn.
- Phiếu mượn trải đều trong 6 tháng gần nhất, có cả phiếu đã trả, đang mượn, quá hạn và có tiền phạt, để màn hình thống kê có dữ liệu khi demo.
- Vài yêu cầu đặt trước và yêu cầu gia hạn ở các trạng thái khác nhau.

## 9. Thứ tự triển khai

| Giai đoạn | Nội dung | UC |
|---|---|---|
| 1. Nền tảng | Tạo solution, cấu hình MongoDB, conventions, `MongoDbContext`, `DbInitializer` (index), layout Bootstrap | |
| 2. Tài khoản | Cookie auth, đăng nhập, đăng xuất, đổi mật khẩu, seed tài khoản admin | UC01–UC03 |
| 3. Danh mục | Thể loại, sách | UC04, UC05 |
| 4. Độc giả | Thêm độc giả kèm tạo tài khoản, khóa/mở thẻ, gia hạn thẻ, đặt lại mật khẩu, `CounterService` | UC06 |
| 5. Mượn trả | Bật replica set, lập phiếu mượn, nhận trả sách, tính phạt | UC07, UC08 |
| 6. Phía độc giả | Tìm sách, xem sách đang mượn và lịch sử | UC13, UC14 |
| 7. Gia hạn | Gửi yêu cầu, duyệt, gia hạn tại quầy | UC09, UC15 |
| 8. Đặt trước | Đặt, hủy, duyệt, nhắc danh sách người chờ khi trả sách, tự hoàn tất khi mượn | UC10, UC16 |
| 9. Tra cứu, thống kê | Tìm kiếm của thủ thư, các aggregation, Chart.js | UC11, UC12 |
| 10. Hoàn thiện | Seed đầy đủ, kiểm thử theo dòng sự kiện phụ của từng UC, chụp màn hình cho báo cáo | |

## 10. Quy ước code

- Đặt tên class, property theo PascalCase tiếng Anh. Nội dung hiển thị trên giao diện và thông báo dùng tiếng Việt.
- Controller không truy cập trực tiếp `IMongoCollection`, chỉ gọi Service.
- Không dùng Area. Controller của độc giả đặt tên khác controller của thủ thư (`Catalog` thay vì `Books`, tiền tố `My...` cho dữ liệu cá nhân) để không trùng tên.
- Service trả về `ServiceResult` (thành công hoặc lỗi kèm thông báo). Controller đưa thông báo vào `TempData` để `_Alert` hiển thị.
- Kiểm tra dữ liệu form bằng DataAnnotations trên ViewModel. Kiểm tra quy định nghiệp vụ (trùng CCCD, vượt số sách...) đặt trong Service.
- Dùng `async/await` cho mọi lệnh gọi MongoDB.
