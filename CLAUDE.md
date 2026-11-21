# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Đồ án môn NoSQL: **Hệ thống quản lý mượn trả sách**, dùng ASP.NET Core MVC (C#, .NET 10) + MongoDB.
Giao tiếp với người dùng bằng tiếng Việt.

## ⚠️ Việc cần làm trước khi code (nhắc người dùng ở đầu mỗi phiên cho đến khi xong)

Kiểm tra lại bằng lệnh trước khi nhắc. Khi một mục đã xong, đánh dấu `[x]` và ghi ngày.

- [x] **Cài .NET 10 SDK.** (xong 21/11/2026, SDK 10.0.404 tại `C:\Program Files\dotnet`) Kiểm tra: `dotnet --list-sdks`.
- [x] **Bật service MongoDB.** (xong 21/11/2026, Running, Startup Automatic) MongoDB 8.3 tại `C:\Program Files\MongoDB\Server\8.3`.
- [x] **Cấu hình replica set 1 node** (xong 21/11/2026, `rs0` là PRIMARY, file cấu hình gốc sao lưu ở `mongod.cfg.bak`). Bắt buộc để dùng transaction; chi tiết ở mục 7.1 `thiet-ke-ky-thuat.md`.
- [ ] **Cài MongoDB Compass.** mongosh 2.14.3 đã cài và vào PATH ngày 21/11/2026, còn thiếu Compass.
- [x] **Tạo project** (xong 21/11/2026: `LibraryManagement.slnx`, net10.0, MongoDB.Driver 3.14.1, BCrypt.Net-Next 4.2.0).

## Lệnh thường dùng

Chạy từ `LibraryManagement/LibraryManagement.Web/`:

```powershell
dotnet build                                   # build
dotnet run --launch-profile http               # chạy tại http://localhost:5101 (profile https: https://localhost:7195)
Get-Process LibraryManagement.Web | Stop-Process -Force   # dừng app đang chạy trước khi build lại (app giữ khóa file .dll)
```

- Chưa có project test. Kiểm thử thủ công theo các dòng sự kiện phụ (3a, 4b...) của từng UC trong `dac-ta-use-case.md`.
- mongosh: chỉ có trong PATH của PowerShell mở mới (Git Bash không thấy). Nếu PowerShell báo không tìm thấy, nạp lại PATH:
  `$env:Path = [Environment]::GetEnvironmentVariable('Path','User') + ';' + [Environment]::GetEnvironmentVariable('Path','Machine')`.
  Chuỗi kết nối: `mongodb://localhost:27017/library_management?replicaSet=rs0`. Khi đối số `--eval` có dấu ngoặc kép, viết script ra file rồi chạy `mongosh --file`.
- Khi khởi động, `DbInitializer` tạo index (chạy lại không lỗi) và **chỉ seed khi collection `users` rỗng**. Muốn seed lại thì xóa database `library_management`.
- Tài khoản seed: Thủ thư `admin` / `admin123`; độc giả `DG000001`, `DG000002` với mật khẩu `LibraryRules.DefaultPassword`.

## Tài liệu (nguồn sự thật)

Nằm trong thư mục `LibraryManagement/LibraryManagement.Web/Doc/`:

| File | Nội dung |
|---|---|
| `quan-ly-muon-tra-sach.md` | Mô tả đề tài ban đầu. Phần database trong file này **đã cũ**, theo `thiet-ke-ky-thuat.md` |
| `dac-ta-use-case.md` | Quy định nghiệp vụ QĐ01–QĐ10, quy trình nghiệp vụ, đặc tả 16 use case UC01–UC16 |
| `thiet-ke-ky-thuat.md` | Công nghệ, cấu trúc thư mục, ánh xạ UC → Controller, schema MongoDB, index, claim (6.3), quy ước ngày giờ (6.4), giao diện dùng chung (6.5), transaction, aggregation, thứ tự triển khai |
| `phan-cong.md` | Phân công 2 người (A, B), file nào ai sửa, 5 hàm giao kèo giữa `LoanService` và `ReservationService`, quy tắc Git |
| `CheckPoint_Member1/checklist-thanh-vien-a.md` | Checklist tiến độ của thành viên A (người dùng đang làm phần A). Làm xong mục nào thì đánh `[x]` kèm ngày |

Ảnh mẫu bảng đặc tả use case (`Ảnh chụp màn hình 2026-11-13 160147.png`) nằm ở thư mục gốc.

Code phải khớp với các tài liệu này. Khi thay đổi nghiệp vụ hoặc schema, **cập nhật tài liệu trước**, rồi mới sửa code.

## Kiến trúc

Một project MVC, chia lớp bằng thư mục: Controller → Service → `MongoDbContext` → MongoDB.

- **Khởi động** (`Program.cs`): `MongoConventions.Register()` phải chạy trước khi tạo collection (camelCase, enum lưu string, bỏ qua field thừa). `IMongoClient` và `MongoDbContext` là singleton; mọi service là scoped và **đã đăng ký sẵn tất cả**, để B không phải sửa `Program.cs`. `LoanService` và `ReservationService` gọi lẫn nhau, nên `LoanService` nhận `Lazy<ReservationService>` để tránh vòng phụ thuộc DI.
- **Kiểu dữ liệu trong Models:** Id là `string` kèm `[BsonRepresentation(BsonType.ObjectId)]` (riêng `counters` có `_id` là tên bộ đếm). Tiền là `long` (đồng). Field kiểu **ngày** (`dueDate`, `expiryDate`...) lưu 00:00 giờ VN quy ra UTC; field kiểu **thời điểm** lưu `UtcNow`. Mọi phép tính ngày đi qua `Services/DateHelper.cs` (`TodayVn`, `VnDateToUtc`, `ToVnDate`, `StartOfTodayVnUtc`, `DaysOverdue`). Tên hiển thị tiếng Việt của enum: `EnumDisplay.ToVn()` trong `Models/Enums.cs`.
- **Kết quả service:** trả `ServiceResult` / `ServiceResult<T>`. `Field` là tên property của ViewModel để báo lỗi đúng ô: controller gọi `ModelState.AddModelError(result.Field ?? "", result.Message)`. Trường hợp thành công thì đặt `TempData["Success"]` / `TempData["Error"]`, `_Alert` sẽ hiển thị. Trong transaction thì ném `BusinessException` để transaction tự hủy.
- **Xác thực:** cookie auth, role `Librarian` / `Reader`. Claim gồm `NameIdentifier` (userId), `Name` (họ tên), `Role`, `ReaderId` (độc giả), và `CardNotice` (thẻ khóa / hết hạn, chỉ để hiển thị). Controller của độc giả lấy `ReaderId` từ claim, không nhận từ form. `HomeController.Index` chuyển hướng theo role: Thủ thư → `Dashboard`, Độc giả → `Catalog`.
- **Giao diện** (mục 6.5 thiết kế kỹ thuật): một `_Layout` cho cả hai vai trò. Chưa đăng nhập thì chỉ có khung trắng giữa nền navy, view chỉ viết form. Đã đăng nhập thì có sidebar (`_LibrarianMenu` / `_ReaderMenu` theo role), topbar có dropdown tài khoản, thanh `CardNotice`, `_Alert`, tiêu đề lấy từ `ViewData["Title"]`. Nút bên phải tiêu đề đặt trong `@section PageActions`. Class dùng chung trong `wwwroot/css/site.css`: `content-card`, `toolbar`, `stat-card`, `info-list`, `empty-state`, badge `status-{enum viết thường}` (thêm `status-overdue`, `status-expired`). Màu đổi ở biến `:root`. Icon dùng Bootstrap Icons qua CDN.
- **Link menu tới controller chưa làm sẽ 404.** Menu tạo sẵn mọi link, kể cả phần của B. Hiện chỉ có `Home` và `Account` (UC01–03).
- Thư mục `ThamKhảo/` nằm **trong** project web và chỉ chứa vài file của đồ án cũ. Nếu chép file `.cs` vào đây, SDK sẽ compile luôn. Mã nguồn đầy đủ của đồ án cũ Shop-Flower (ASP.NET MVC 5, dùng tham khảo giao diện) nằm ngoài repo, tại `D:\Flower\Shop-Flower`.

## Phân công và Git

- Mỗi file chỉ một người sửa (xem `phan-cong.md`). File dùng chung (`Program.cs`, `Models/`, `Data/`, `appsettings.json`, `ServiceResult`, layout, menu, `site.css`) chỉ A sửa. A và B chỉ gọi nhau qua 5 hàm giao kèo.
- Mỗi UC làm trên một nhánh riêng (ví dụ `uc07-lap-phieu-muon`), xong UC nào gộp vào `main` ngay. Chỉ commit hoặc push khi người dùng yêu cầu.

## Các quyết định đã chốt

- Không có chức năng tự đăng ký: tài khoản độc giả do Thủ thư cấp (UC06).
- Gia hạn online do Thủ thư duyệt (UC09, UC15).
- Đặt trước: chỉ đặt khi sách đang hết; **không giữ sách riêng**; 5 trạng thái `Pending, Approved, Rejected, Completed, Cancelled`. Khi độc giả mượn đúng sách đã đặt, yêu cầu tự chuyển `Completed`.
- Cho phép trả sách từng phần trong một phiếu.
- **Không dùng Area.** Controller hai vai trò nằm chung `Controllers/`, phân quyền bằng `[Authorize(Roles = ...)]`. Controller của độc giả: `Catalog`, `MyLoans`, `MyReservations`.

## Quy ước code

- Controller → Service → MongoDB. Controller không dùng trực tiếp `IMongoCollection`.
- Quy định nghiệp vụ (số sách tối đa, số ngày mượn, mức phạt, độ dài mật khẩu...) đọc từ `LibraryRules` trong `appsettings.json` qua `IOptions<LibraryRules>`, không viết cứng.
- Kiểm tra dữ liệu form bằng DataAnnotations trên ViewModel; kiểm tra quy định nghiệp vụ (trùng CCCD, vượt số sách...) đặt trong Service.
- Trạng thái "quá hạn" và "thẻ hết hạn" tính khi truy vấn, không lưu trong database.
- Lập phiếu mượn và nhận trả sách chạy trong transaction. Trừ số lượng sách bằng update có điều kiện `availableQuantity > 0`.
- Mọi action POST dùng `[ValidateAntiForgeryToken]`. Ngoại lệ duy nhất là `Logout`: dùng `[IgnoreAntiforgeryToken]` và tự kiểm tra token khi còn đăng nhập, để khi phiên đã hết hạn (UC02, bước 1a) không bị lỗi 400.
- Tên class, property tiếng Anh PascalCase; giao diện và thông báo tiếng Việt.
- Ghi mã UC (ví dụ `// UC07`) ở đầu các action và method service tương ứng.

## Phạm vi công việc

Người dùng muốn đi từng bước. Chỉ tạo hoặc viết code khi được yêu cầu rõ ràng; khi được bảo "chỉ tạo thư mục" thì không tạo file code.
