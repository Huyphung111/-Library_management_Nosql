# Checklist công việc thành viên B

Danh sách theo `phan-cong.md`. Nghiệp vụ chi tiết xem `dac-ta-use-case.md`, cách cài đặt xem `thiet-ke-ky-thuat.md`, quy ước giao diện xem mục 6.5 thiết kế kỹ thuật.

Cách dùng: làm xong mục nào thì đổi `[ ]` thành `[x]` và ghi ngày ở cuối dòng, ví dụ `[x] ... (22/11/2026)`.

Test case: viết cho từng UC của B trong thư mục [TestCase/](../TestCase/README.md), theo mẫu file của A (mã `TCxx-yy`, cột "Dòng SK" ghi bước trong đặc tả). Dòng "Kiểm thử UC..." dưới đây chỉ đánh `[x]` khi mọi test case của UC đó đều **Đạt**.

## Chuẩn bị môi trường trên máy B

- [ ] Cài .NET 10 SDK, kiểm tra bằng `dotnet --list-sdks`
- [ ] Cài MongoDB 8.x, bật service MongoDB, đặt Startup type = Automatic
- [ ] Cấu hình replica set `rs0` và chạy `rs.initiate()` (mục 7.1 thiết kế kỹ thuật). Không có replica set thì transaction ở UC07, UC08 sẽ lỗi
- [ ] Cài mongosh và MongoDB Compass
- [ ] Clone repo, tạo nhánh làm việc từ `dev`: `git fetch origin` rồi `git switch -c member2 origin/dev`
- [ ] Nhập dữ liệu mẫu: trong `Data/MongoData/` chạy `mongosh "mongodb://localhost:27017/library_management?replicaSet=rs0" --file import.js` (xem `Data/MongoData/README.md`)
- [ ] Chạy `dotnet run --launch-profile http`, mở http://localhost:5101, đăng nhập `admin` / `admin123` được
- [ ] Đọc mục 6.5 thiết kế kỹ thuật (giao diện dùng chung) và đồng ý bảng màu với A (navy `#1e3a5f` + vàng đồng `#c8963e`)

## Nhận bàn giao từ A (đã có trên `dev`, B dùng, không sửa)

- Project, `Program.cs` đã đăng ký **tất cả** service của B; `Models/`, `Data/` (index), `Options/LibraryRules`, `DateHelper`, `ServiceResult` (có `Field` để báo lỗi đúng ô), `CounterService`
- Class rỗng `BookService`, `CategoryService`, `ReservationService`, `SearchService`, `ReportService` để B viết tiếp
- `_Layout`, `_LibrarianMenu`, `_ReaderMenu` đã có sẵn link tới mọi trang của B; `_Alert`; class CSS dùng chung trong `site.css`
- UC01–UC03: đăng nhập, đăng xuất, đổi mật khẩu. Sau khi đăng nhập, Thủ thư được chuyển tới `/Dashboard`, Độc giả tới `/Catalog`: cả hai là trang của B, **đang báo 404** cho tới khi B làm

**Cách viết một view cho khớp layout:** đặt `ViewData["Title"]`; nút bên phải tiêu đề đặt trong `@section PageActions { ... }`; nội dung bọc trong `<div class="content-card">`; thanh tìm kiếm dùng `toolbar`; trạng thái dùng `<span class="badge status-@x.ToString().ToLower()">@x.ToVn()</span>`; thông báo đặt vào `TempData["Success"]` / `TempData["Error"]`; script riêng (ví dụ Chart.js) đặt trong `@section Scripts`.

**File B sở hữu:** controller `Books`, `Categories`, `Catalog`, `Reservations`, `MyReservations`, `Search`, `Reports`, `Dashboard`; service `BookService`, `CategoryService`, `ReservationService`, `SearchService`, `ReportService`; `Views/` và `ViewModels/` của các controller đó; phần seed trong `Data/DbInitializer.cs` (A đã giao).

## Cần thống nhất với A trước khi code

Đã chốt cả 3 điểm ngày 21/11/2026, chi tiết ở `phan-cong.md` mục 3 ("Các điểm A và B đã chốt").

- [x] Kiểu trả về của `GetWaitingListAsync(bookIds)`: đổi sang `Task<List<WaitingReader>>`. Lớp `WaitingReader` khai báo trong `Services/ReservationService.cs` (`ReservationId`, `BookId`, `BookTitle`, `ReaderId`, `CardNumber`, `FullName`, `Phone`, `Email`, `ReservedAt`). **B** ghép họ tên, SĐT, email từ `readers`; A chỉ hiển thị. A đã đổi sẵn chữ ký trong bản tạm, build thành công (21/11/2026)
- [x] Giao diện trang Tìm sách (UC13) dạng lưới thẻ sách: A đã thêm `book-grid`, `book-card`, `book-cover` (+ `alt`), `book-body`, `book-title`, `book-meta`, `book-foot`, `cat-tag`, `stock in` / `stock out`, `result-info` vào `site.css`; cách dùng ở mục 6.5 thiết kế kỹ thuật và chú thích trong `site.css` (21/11/2026)
- [x] Nội dung trang Dashboard: 4 thẻ số liệu, khung "Việc cần xử lý", 2 nút thao tác nhanh; số liệu qua `ReportService.GetDashboardAsync()` (21/11/2026)

## Đợt 1: Danh mục và tìm sách

### Dashboard (đề xuất làm sớm, vì sau đăng nhập Thủ thư đang bị 404)
- [x] `DashboardController.Index` với `[Authorize(Roles = "Librarian")]` (22/11/2026)
- [x] `ReportService.GetDashboardAsync()`: chỉ đọc dữ liệu, trả về các con số bên dưới (22/11/2026)
- [x] 4 thẻ `stat-card`: Đầu sách (số đầu sách / tổng số cuốn), Độc giả đang hoạt động, Phiếu đang mượn, Phiếu quá hạn (thẻ màu đỏ, tính khi truy vấn) (22/11/2026)
- [x] Khung "Việc cần xử lý", mỗi dòng có link: gia hạn chờ duyệt (→ `Renewals`), đặt trước chờ duyệt (→ `Reservations`), đặt trước đã duyệt có sách về cần liên hệ (→ `Reservations`) (22/11/2026)
- [x] Nút nhanh "Lập phiếu mượn" (→ `Loans/Create`), "Nhận trả sách" (→ `Loans/Return`) đặt trong `@section PageActions` (22/11/2026)
- [ ] Gộp vào `dev`

### UC05 Quản lý thể loại (nhánh `uc05-the-loai`)
- [x] `CategoryService`: danh sách kèm **số đầu sách** của mỗi thể loại, thêm, sửa, xóa (22/11/2026)
- [x] Tên không trống, không trùng. Index `name` unique có collation `vi` strength 2 nên "văn học" trùng "Văn học": bắt lỗi trùng khóa và báo đúng ô tên (`ServiceResult.Fail(..., "Name")`) (22/11/2026)
- [x] Xóa chỉ khi không còn sách thuộc thể loại (QĐ09); nếu còn thì báo số sách còn thuộc thể loại (22/11/2026)
- [x] `CategoriesController`: `Index`, `Create`, `Edit`, `Delete` (POST, hỏi xác nhận trước) (22/11/2026)
- [x] ViewModel form thể loại, views danh sách và form (22/11/2026)
- [ ] Kiểm thử: tên trống, tên trùng kể cả khác hoa thường (6a); sửa (3a); xóa thể loại còn sách (3b); xóa thành công
- [ ] Gộp vào `dev`

### UC04 Quản lý sách (nhánh `uc04-quan-ly-sach`)
- [x] `BookService`: danh sách (tên, tác giả, thể loại, số lượng tổng, số còn lại), thêm, sửa, xóa (22/11/2026)
- [x] Thêm: tên không trống, ít nhất 1 tác giả và 1 thể loại, giá và số lượng tổng là số dương; `availableQuantity` = `totalQuantity` (22/11/2026)
- [x] Sửa (3a): số lượng tổng mới không nhỏ hơn số đang cho mượn (`total - available`), số còn lại đổi theo mức chênh lệch. Dùng update có điều kiện để không lệch khi đang có người mượn cùng lúc (22/11/2026)
- [x] Xóa (3b): gọi `LoanService.HasOpenLoanForBookAsync(bookId)`; có phiếu mở thì báo không thể xóa (QĐ09) (22/11/2026)
- [x] `BooksController`: `Index`, `Create`, `Edit`, `Delete`; thể loại chọn từ danh mục có sẵn (22/11/2026)
- [x] `ViewModels/Books`: `BookFormVM`, `BookListVM` (22/11/2026)
- [ ] Kiểm thử: dữ liệu không hợp lệ (6a); giảm số lượng tổng dưới số đang cho mượn; xóa sách đang có phiếu mở (3b); xóa thành công. **Lưu ý:** `HasOpenLoanForBookAsync` của A đang tạm trả `false`, thử lại test xóa sau khi A viết thật
- [ ] Gộp vào `dev`

### UC13 Tìm kiếm sách (nhánh `uc13-tim-sach`)
- [x] `BookService`: tìm theo tên hoặc tác giả (text index trên `title`, `authors`), lọc theo thể loại; tình trạng *Còn* / *Hết* theo `availableQuantity` (22/11/2026)
- [x] `CatalogController` với `[Authorize(Roles = "Reader")]`: `Index`, `Details` (22/11/2026)
- [x] Trang `Index` dùng lưới thẻ sách đã có trong `site.css` (`book-grid`, `book-card`..., mục 6.5 thiết kế kỹ thuật): sách hết dùng `stock out` và nút "Đặt trước" (22/11/2026)
- [x] Trang chi tiết: tên, tác giả, thể loại, NXB, năm xuất bản, số cuốn còn. Nút "Đặt trước" **chỉ hiện khi sách hết**; sách còn thì hướng dẫn đến quầy mượn (UC16 bước 1a) (22/11/2026)
- [ ] Kiểm thử: không tìm thấy (3a); tìm theo tác giả; lọc thể loại; sách hết có nút Đặt trước, sách còn thì không
- [ ] Gộp vào `dev`

## Đợt 2: Đặt trước và hàm giao kèo

### UC16 Đặt trước sách (nhánh `uc16-dat-truoc`)
- [x] `ReservationService`: kiểm tra `availableQuantity == 0`, thẻ *Hoạt động* (và chưa hết hạn, khớp thông báo `CardNotice` của UC01), chưa có yêu cầu *Chờ duyệt* / *Đã duyệt* cho sách này (QĐ08), không đang mượn sách này (gọi `LoanService.IsBorrowingAsync`) (22/11/2026)
- [x] Tạo yêu cầu: `reservedAt` = bây giờ (UTC), *Chờ duyệt*, lưu bản sao `bookTitle` (22/11/2026)
- [x] Hủy (1b): yêu cầu *Chờ duyệt* hoặc *Đã duyệt* của chính mình chuyển *Đã hủy* (22/11/2026)
- [x] `MyReservationsController` với `[Authorize(Roles = "Reader")]`: `Index`, `Create` (POST), `Cancel` (POST). `ReaderId` lấy từ claim, không nhận từ form (22/11/2026)
- [x] View xác nhận: thông tin sách và lưu ý "thư viện sẽ liên hệ khi có sách, sách không được giữ riêng" (22/11/2026)
- [ ] Kiểm thử: sách còn thì không có nút (1a); hủy (1b); thẻ khóa / hết hạn, đã có yêu cầu, đang mượn sách này (2a); không hủy được yêu cầu của người khác. **Lưu ý:** `IsBorrowingAsync` của A đang tạm trả `false`
- [ ] Gộp vào `dev`

### UC10 Xử lý đặt trước (nhánh `uc10-xu-ly-dat-truoc`)
- [x] `ReservationService`: danh sách theo trạng thái; `$lookup` sang `books` để đánh dấu yêu cầu *Đã duyệt* có sách đã về kho (`availableQuantity > 0`) (22/11/2026)
- [x] Chi tiết yêu cầu: độc giả, SĐT, tình trạng thẻ, sách, ngày đặt, số còn lại; cảnh báo khi thẻ khóa hoặc hết hạn (4a); yêu cầu *Đã duyệt* có sách về thì hiện SĐT và email để liên hệ (3a, dùng lại `WaitingReader`) (22/11/2026)
- [x] Duyệt, Từ chối (kèm lý do, 5a), Hủy yêu cầu *Đã duyệt* (kèm lý do, 3b): đổi `status`, lưu `reason`, `processedBy` (22/11/2026)
- [x] `ReservationsController` với `[Authorize(Roles = "Librarian")]`: `Index`, `Approve`, `Reject`, `Cancel` (22/11/2026)
- [ ] Kiểm thử: duyệt; từ chối kèm lý do; hủy yêu cầu đã duyệt; cảnh báo thẻ khóa; đánh dấu sách đã về
- [ ] Gộp vào `dev`

### 3 hàm giao kèo cho A (đổi từ giá trị giả sang code thật)
- [x] `CompleteForLoanAsync(session, readerId, bookIds, loanId)`: **chạy trong `session` A truyền vào**; `UpdateMany` các yêu cầu *Chờ duyệt* / *Đã duyệt* của độc giả cho các sách này sang *Hoàn tất*, ghi `completedLoanId` (22/11/2026)
- [x] `GetWaitingListAsync(bookIds)`: yêu cầu *Đã duyệt* của các sách, sắp theo `reservedAt` tăng dần, trả về `List<WaitingReader>` với họ tên, SĐT, email lấy từ `readers` (dùng `$lookup` hoặc truy vấn `readers` theo danh sách `readerId`) (22/11/2026)
- [x] `HasWaitingAsync(bookIds)`: có yêu cầu *Chờ duyệt* hoặc *Đã duyệt* cho sách nào trong danh sách không (22/11/2026)
- [ ] Gộp vào `dev` và báo A đã có hàm thật

## Đợt 3: Tra cứu và thống kê

### UC11 Tìm kiếm (nhánh `uc11-tim-kiem`)
- [x] `SearchController.Index` với tham số `type` = `book` / `reader` / `loan` (menu đã có sẵn 3 link) (22/11/2026)
- [x] Sách: tên, tác giả, thể loại. Độc giả: mã thẻ, họ tên, SĐT, CCCD. Phiếu mượn: mã phiếu, mã thẻ, trạng thái (kể cả *Quá hạn* tính khi truy vấn), khoảng ngày mượn (22/11/2026)
- [x] Chọn một kết quả thì mở trang chi tiết có sẵn (chốt với A link tới trang độc giả, phiếu mượn của A) (22/11/2026)
- [ ] Kiểm thử: không có kết quả (5a); từng loại đối tượng và bộ lọc
- [ ] Gộp vào `dev`

### UC12 Thống kê (nhánh `uc12-thong-ke`)
- [x] Thử 5 pipeline ở mục 7.4 thiết kế kỹ thuật trong tab Aggregations của Compass trước (22/11/2026)
- [x] `ReportService`: top sách mượn nhiều, phiếu quá hạn, lượt mượn theo tháng, theo thể loại, tổng tiền phạt (22/11/2026)
- [x] Ngày lưu là 00:00 giờ VN quy ra UTC (17:00 hôm trước): khi `$group` theo `$month` / `$dateToString` phải truyền `timezone: "Asia/Ho_Chi_Minh"`, nếu không số liệu bị lệch sang tháng trước. Phiếu quá hạn so với `DateHelper.StartOfTodayVnUtc()` (22/11/2026)
- [x] `ReportsController`: `TopBooks`, `Overdue`, `ByMonth`, `ByCategory`, `Fines`; `ViewModels/Reports` (22/11/2026)
- [x] Biểu đồ Chart.js: nhúng trong `@section Scripts` của từng view (không sửa layout) (22/11/2026)
- [ ] Kiểm thử: ngày bắt đầu sau ngày kết thúc (3a); khoảng thời gian không có dữ liệu (4a); số liệu khớp với đếm tay trong Compass
- [ ] Gộp vào `dev`

## Đợt 4: Seed đầy đủ và kiểm thử chéo

### Seed đầy đủ (mục 8 thiết kế kỹ thuật)
- [x] Thay seed tối thiểu trong `DbInitializer.SeedAsync`: 1 thủ thư, 5–6 thể loại, khoảng 30 đầu sách, 10 độc giả (1 thẻ khóa, 1 thẻ hết hạn) (22/11/2026)
- [x] Phiếu mượn trải đều 6 tháng gần nhất: đã trả, đang mượn, quá hạn, có tiền phạt; đúng cấu trúc `Loan` (`items`, `renewalRequests`) và dùng `CounterService` để sinh mã `PM...` (22/11/2026)
- [x] Vài yêu cầu đặt trước và gia hạn ở đủ các trạng thái (22/11/2026)
- [ ] Seed chỉ chạy khi `users` rỗng: xóa database rồi chạy lại để thử
- [ ] Xuất lại dữ liệu mẫu bằng `Data/MongoData/export.js`, cập nhật bảng trong `Data/MongoData/README.md`, commit
- [ ] Gộp vào `dev`, báo A nhập lại dữ liệu

### Kiểm thử chéo phần của A (dùng các file trong `Doc/TestCase/`)
- [ ] UC01–UC03 Tài khoản
- [ ] UC06 Quản lý độc giả
- [ ] UC07 Lập phiếu mượn, đặc biệt yêu cầu đặt trước tự chuyển *Hoàn tất*
- [ ] UC08 Nhận trả sách, đặc biệt danh sách người chờ
- [ ] UC09, UC15 Gia hạn, đặc biệt không cho gia hạn khi có người đặt trước
- [ ] UC14 Sách đang mượn và lịch sử
- [ ] Ghi lỗi tìm được và gửi A

## Quy tắc mỗi ngày

- `git fetch origin` rồi `git merge origin/dev` vào nhánh `member2` trước khi code
- Chỉ sửa file của B. Cần thêm field, link menu, class CSS hay đổi file dùng chung thì nhắn A
- Đổi nghiệp vụ hoặc schema: sửa `Doc/` trước, báo A, rồi mới sửa code
- Ghi mã UC (ví dụ `// UC04`) ở đầu action và method service; quy định nghiệp vụ đọc từ `LibraryRules`, không viết cứng
